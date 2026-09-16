# Exercise: .NET Aspire -- Local Orchestration and Azure Deployment

## Overview

You're going to build the same small app twice: once by typing it yourself
into the empty workspace projects, guided by this file step by step, and
once more by reading the fully-worked version in `solution/` whenever you
get stuck or want to double check something.

The app is a two-service storefront:

```
 Storefront  ---->  CatalogApi  ---->  Postgres (catalogdb)
   :5302              :5301  \
                               ---->  Redis (cache, output caching)
```

`Storefront` calls `CatalogApi` over HTTP to render a product list.
`CatalogApi` reads products from Postgres and caches the `/products`
response in Redis. None of this is unusual -- what's unusual is that you
will never write a connection string, a health check, or a port number by
hand. That's the point of Aspire: the **AppHost** project orchestrates every
piece and injects everything the other projects need to find each other.

Part A builds and runs this entirely on your machine (Docker required, no
Azure). Part B takes the same app and deploys it to a real Azure
subscription with `azd` (your own machine, your own subscription -- not
available in the environment this exercise was authored in).

## Prerequisites

- Read `solution/ServiceDefaults/Extensions.cs`, `solution/CatalogApi/*.cs`,
  `solution/Storefront/Program.cs`, `solution/AppHost/Program.cs`, and
  `solution/AppHost/manifest.json` once before starting. You are about to
  retype most of this; know what you're aiming for.
- Docker Desktop (or another Docker-compatible engine) running, for Part A.
- .NET 9 SDK (already required by the rest of this repo).
- For Part B only: an Azure subscription and the Azure Developer CLI
  (`azd`). See `GETTING_STARTED.md` for the install link -- Part B is
  written so you can read every step without having either.

---

# Part A: Aspire Locally

## A.1 -- AppHost Fundamentals

Open `AppHost/Program.cs` in your workspace. It should already look like
this (it's the starting point, not something you need to create):

```csharp
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CatalogApi>("catalogapi");
builder.AddProject<Projects.Storefront>("storefront");

builder.Build().Run();
```

**Your Task:** Run it.

```bash
dotnet run --project AppHost
```

It will fail to fully come up yet (`CatalogApi` and `Storefront` aren't
wired for their real dependencies until A.3/A.4), but it will start and
open the Aspire dashboard in your browser -- confirm that happens before
moving on.

**Why this works at all:** `DistributedApplication.CreateBuilder(args)` is
the Aspire equivalent of `WebApplication.CreateBuilder(args)` -- it builds a
special kind of host whose only job is to start and supervise *other*
processes and containers, not to serve HTTP itself. `AddProject<Projects.CatalogApi>("catalogapi")`
references CatalogApi not by a file path string, but through `Projects.CatalogApi`
-- a type the Aspire MSBuild SDK source-generates from every project you add
as an `IsAspireProjectResource="true"` `ProjectReference` in `AppHost.csproj`.
That generated type is how the AppHost knows CatalogApi's executable path,
target framework, and launch profiles at orchestration time, with full
compile-time safety: rename the project and this line fails to compile
instead of failing at runtime.

**Questions to think about:**
1. What would happen if you passed a plain string path to `AddProject`
   instead of using the generated `Projects.CatalogApi` type? (Hint: check
   older Aspire samples/tutorials online -- this is exactly how it used to
   work before the source generator existed.)
2. `builder.Build().Run()` never returns while the AppHost is running. What
   process is actually "running" here -- is it CatalogApi's code, or
   something else?

## A.2 -- ServiceDefaults

Every project below (`CatalogApi`, `Storefront`, and even the AppHost
implicitly) shares one small project: `ServiceDefaults`. Its entire purpose
is to be the one place that configures OpenTelemetry, health checks,
service discovery, and HTTP resilience identically everywhere, instead of
four copy-pasted blocks of startup code across four projects.

**Your Task:** Replace the `// TODO` body of `ServiceDefaults/Extensions.cs`
in your workspace with:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath);

        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
```

**Why this shape:** `AddServiceDefaults<TBuilder>` is generic over
`IHostApplicationBuilder` so it works on both `WebApplicationBuilder`
(CatalogApi, Storefront) and any future non-web `HostApplicationBuilder`
you might add. Four things get wired in one call:

- **OpenTelemetry** -- traces and metrics for ASP.NET Core and outgoing
  `HttpClient` calls, exported over OTLP *only if* `OTEL_EXPORTER_OTLP_ENDPOINT`
  is set. You never set that variable yourself -- the AppHost sets it on
  every resource it orchestrates, pointing at its own dashboard. Locally
  that means free tracing; after Part B's deployment, the exact same code
  points at Azure Monitor instead, because `azd`/Aspire's Azure publisher
  sets the same variable to an Application Insights endpoint. Zero code
  changes between "local" and "deployed."
- **Health checks** -- a `"self"` check tagged `"live"` that has no
  dependencies. `/health` runs every check; `/alive` only runs the ones
  tagged `"live"`. This is the same liveness/readiness split documented in
  `06-CloudNative/README.md`'s "Three Probes" section: liveness must never
  depend on a database, or a database outage makes an orchestrator restart
  every healthy pod for no reason.
- **Service discovery** (`AddServiceDiscovery`) -- lets HTTP clients resolve
  names like `http://catalogapi` to a real address at runtime. See A.4.
- **HTTP resilience** (`AddStandardResilienceHandler`) -- retries, a
  circuit breaker, and timeouts applied to every `HttpClient` created
  through `ConfigureHttpClientDefaults`, with zero per-client setup.

**Your Task (continued):** Reference this project from `CatalogApi` and
`Storefront` if it isn't already (`ServiceDefaults.csproj` should be a
`ProjectReference` in both), and call `builder.AddServiceDefaults()` as the
very first line after `WebApplication.CreateBuilder(args)` in both
projects' `Program.cs` -- you'll write the rest of those files in A.3/A.4.

**Questions to think about:**
1. Why is `MapDefaultEndpoints()` a separate call from `AddServiceDefaults()`
   instead of being folded into it?
2. What's the difference between `/health` and `/alive` in this file, and
   which one would you point a Kubernetes liveness probe at?

## A.3 -- Container Resources and Client Integrations

This is the biggest step: CatalogApi gets a real database and a real cache.

**Your Task:** Create `CatalogApi/Product.cs`:

```csharp
namespace CatalogApi;

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
}
```

Create `CatalogApi/CatalogDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace CatalogApi;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
```

Replace `CatalogApi/Program.cs` with:

```csharp
using CatalogApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<CatalogDbContext>("catalogdb");

builder.AddRedisOutputCache("cache");

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseOutputCache();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.EnsureCreatedAsync();

    if (!await db.Products.AnyAsync())
    {
        var seedCount = builder.Configuration.GetValue("SeedProductCount", 10);

        for (var i = 1; i <= seedCount; i++)
        {
            db.Products.Add(new Product { Name = $"Product {i}", Price = i * 4.5m });
        }

        await db.SaveChangesAsync();
    }
}

app.MapGet("/products", async (CatalogDbContext db) =>
    await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync())
    .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(30)).Tag("products"));

app.MapGet("/products/{id:int}", async (int id, CatalogDbContext db) =>
    await db.Products.FindAsync(id) is { } product
        ? Results.Ok(product)
        : Results.NotFound());

app.Run();

public partial class Program;
```

Add `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` and
`Aspire.StackExchange.Redis.OutputCaching` to `CatalogApi.csproj` if they
aren't already there (check -- they might already be present as a starting
point).

Now wire the AppHost side. **Your Task:** add the two packages to
`AppHost.csproj` where the `// TODO Part A.3` comment is:

```xml
<PackageReference Include="Aspire.Hosting.PostgreSQL" Version="9.5.2" />
<PackageReference Include="Aspire.Hosting.Redis" Version="9.5.2" />
```

Then update `AppHost/Program.cs`:

```csharp
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var catalogDb = postgres.AddDatabase("catalogdb");

var cache = builder.AddRedis("cache")
    .WithDataVolume();

var catalogApi = builder.AddProject<Projects.CatalogApi>("catalogapi")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(cache)
    .WaitFor(cache);

builder.AddProject<Projects.Storefront>("storefront");

builder.Build().Run();
```

**Why the names have to match:** `builder.AddNpgsqlDbContext<CatalogDbContext>("catalogdb")`
in CatalogApi and `postgres.AddDatabase("catalogdb")` in the AppHost are
connected by that string. `WithReference(catalogDb)` is what makes the
AppHost inject a `ConnectionStrings__catalogdb` environment variable into
CatalogApi at startup; `AddNpgsqlDbContext` reads that same key. Get the
name wrong in either place and CatalogApi throws at startup trying to
resolve a connection string that was never injected -- there's no silent
fallback.

**Compare this to hand-rolled EF Core.** In `10-EntityFrameworkCore/EfCoreModeling`,
you configured Postgres yourself:

```csharp
builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Library")));
```

That line registers the `DbContext` and nothing else. If you wanted a
health check for it, or OpenTelemetry spans around every query, you added
those separately, by hand, per project. `AddNpgsqlDbContext<CatalogDbContext>("catalogdb")`
registers the `DbContext` **and** a Postgres health check **and** Npgsql's
OpenTelemetry instrumentation, from one call -- because it's Aspire's
*client integration* for Npgsql, not just a thin wrapper over `UseNpgsql`.
Same story for `AddRedisOutputCache("cache")`: one call gets you the output
cache, a Redis health check, and StackExchange.Redis's telemetry. This is
the actual value Aspire adds beyond orchestration -- it isn't only "start
some containers," it's "wire the client libraries that talk to those
containers so the boring, easy-to-forget parts (health checks, telemetry)
are never missing."

**The launchSettings.json gotcha.** Both `CatalogApi/Properties/launchSettings.json`
and `Storefront/Properties/launchSettings.json` already exist in your
workspace with an `http` profile and an `applicationUrl`. This turned out
to matter more than it looks: an Aspire *project resource* (`AddProject<Projects.X>`)
only gets HTTP endpoint bindings (`http`/`https` in the manifest) if the
referenced project has a launch profile that defines one. Without
`launchSettings.json`, `AddProject` still starts the process, but
`WithReference`/service discovery has no endpoint to inject -- the
dependent service's HTTP client would have nothing to resolve `http://catalogapi`
to. This is exactly what generating `solution/AppHost/manifest.json`
confirmed: it only started showing real `services__catalogapi__http__0`
entries under `storefront` after `launchSettings.json` was added to both
projects. If you ever add a new project resource and service discovery to
it mysteriously doesn't work, check this file first.

**Your Task:** Run the AppHost again (`dotnet run --project AppHost`,
Docker running). Open the dashboard and confirm `postgres`, `cache`, and
`catalogapi` all reach a **Running** state (this can take a few seconds the
first time, while the Postgres image pulls). Browse to CatalogApi's `/products`
endpoint directly from the dashboard's endpoint link and confirm you get
back JSON.

**Questions to think about:**
1. `WaitFor(catalogDb)` versus `WithReference(catalogDb)` -- what does each
   one actually do, and what would happen at startup if you kept
   `WithReference` but deleted `WaitFor`?
2. Why does `AddNpgsqlDbContext` need to know the string `"catalogdb"` at
   all -- couldn't Aspire just inject one connection string per app and
   call it a day?

## A.4 -- Service Discovery

**Your Task:** Replace `Storefront/Program.cs` with:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpClient<CatalogApiClient>(client =>
{
    client.BaseAddress = new Uri("http://catalogapi");
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", async (CatalogApiClient catalog) =>
{
    var products = await catalog.GetProductsAsync();
    return Results.Ok(new { message = "Welcome to the storefront", products });
});

app.Run();

public partial class Program;

public class CatalogApiClient(HttpClient httpClient)
{
    public async Task<List<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<List<ProductDto>>("/products", cancellationToken) ?? [];
}

public record ProductDto(int Id, string Name, decimal Price);
```

And finish `AppHost/Program.cs` by adding the reference from `storefront`
to `catalogapi`, plus replicas and the seed parameter (the replicas and
parameter belong to A.6, but add the reference now):

```csharp
builder.AddProject<Projects.Storefront>("storefront")
    .WithReference(catalogApi)
    .WaitFor(catalogApi)
    .WithExternalHttpEndpoints();
```

**Why `http://catalogapi` resolves at all:** there is no DNS entry, no
`appsettings.json` entry, and no hardcoded port anywhere for that hostname.
`ConfigureHttpClientDefaults` in `ServiceDefaults` calls
`http.AddServiceDiscovery()` on every `HttpClient` created in the app,
including the one behind `AddHttpClient<CatalogApiClient>`. When that
client sends a request to `http://catalogapi`, the service discovery
handler intercepts it and looks up `catalogapi` against environment
variables the AppHost injected -- specifically `services__catalogapi__http__0`
(and `__https__0`). You can see these for real: open
`solution/AppHost/manifest.json` and look at the `storefront` resource's
`env` block:

```json
"services__catalogapi__http__0": "{catalogapi.bindings.http.url}",
"services__catalogapi__https__0": "{catalogapi.bindings.https.url}"
```

`WithReference(catalogApi)` on the `storefront` project resource is what
puts those two lines there. Delete that one line and Storefront still
compiles and still starts -- it just throws at request time because
`http://catalogapi` no longer resolves to anything.

**What breaks if you hardcode a port instead:** say you wrote
`client.BaseAddress = new Uri("http://localhost:5301")` instead. It would
work the moment you tested it locally, then silently stop working the
instant `.WithReplicas(2)` is added in A.6 (which of the two instances is
on `:5301`, and what happens to the other one?), and break completely after
Part B's deployment, where CatalogApi doesn't run on `localhost` at all --
it's a separate Container App with its own internal DNS name that you never
chose. Service discovery exists specifically so this class of bug can't
happen: the name `catalogapi` means the same thing in every environment,
and only what it resolves *to* changes.

**Your Task:** Run the AppHost, wait for all resources to report Running,
open Storefront's endpoint from the dashboard, and confirm `GET /` returns
the welcome message with a `products` array.

**Questions to think about:**
1. If you renamed the AppHost resource from `"catalogapi"` to `"catalog-api"`,
   what two places would you need to change for Storefront to still resolve
   it correctly?
2. `WithExternalHttpEndpoints()` is only called on `storefront`, never on
   `catalogapi`. What do you think this controls, and why would you want
   CatalogApi to stay internal-only in a real deployment?

## A.5 -- The Dashboard

No screenshots here -- this is a guided tour of what to click once your app
is running (`dotnet run --project AppHost`, all resources Running).

**Resources tab (the default view):** a table with one row per resource --
`postgres`, `cache`, `catalogapi` (twice, once per replica -- see A.6),
`storefront`. Each row shows a state (`Running`, `Starting`, `Exited`,
`FailedToStart`), a type icon, and its endpoints as clickable links. Click
a `catalogapi` row's endpoint link to open `/products` directly in a new
tab.

**Console logs:** click any resource's name to open its structured log
stream. Notice `catalogapi`'s two replicas each get their own log stream --
this is how you tell them apart, since they share a resource name in the
graph but not a process. Look for the EF Core `EnsureCreatedAsync` and seed
log lines the first time each replica starts against an empty database.

**Traces tab:** make a request to Storefront's `/` endpoint, then switch to
Traces. You should see one trace whose root span is Storefront's incoming
HTTP request, with a child span for the outgoing HTTP call to CatalogApi,
which itself has child spans for the Postgres query (via Npgsql's
OpenTelemetry instrumentation) and, on a cache hit, the Redis call instead.
This one trace spanning three resources, assembled automatically, is what
`ConfigureOpenTelemetry` in `ServiceDefaults` buys you -- no manual
trace-context propagation code was written anywhere in this exercise.

**Metrics tab:** pick `catalogapi` from the resource dropdown and look at
`http.server.request.duration` (ASP.NET Core instrumentation) and the
runtime metrics (GC, thread pool) from `AddRuntimeInstrumentation()`. Watch
a counter change live as you refresh `/products` a few times.

**Resource graph (the icon near the top, sometimes under a "Graph" tab):**
a node-and-edge diagram. Confirm you can see: `storefront` with an edge
pointing at `catalogapi`, `catalogapi` with edges pointing at both
`postgres` and `cache`, and -- once you've done A.6 -- **two separate nodes**
for the two `catalogapi` replicas, both fed by the same edges.

**Your Task:** Generate at least one trace that touches all four resources
(Storefront -> CatalogApi -> Postgres, then a second identical request that
hits Redis instead) and identify the exact span where the second request's
latency drops.

**Questions to think about:**
1. Why does the first request to `/products` show a Postgres span but the
   second (within 30 seconds) doesn't?
2. What would a `FailedToStart` state on the `postgres` resource usually
   mean, and where would you look first to diagnose it?

## A.6 -- Resource Lifecycle

**Your Task:** Add the parameter and replicas to `AppHost/Program.cs`:

```csharp
var seedProductCount = builder.AddParameter("seed-product-count", "10");

// ... postgres, catalogDb, cache as before ...

var catalogApi = builder.AddProject<Projects.CatalogApi>("catalogapi")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(cache)
    .WaitFor(cache)
    .WithEnvironment("SeedProductCount", seedProductCount)
    .WithReplicas(2);
```

Override the default from the command line, without touching code:

```bash
cd AppHost
dotnet user-secrets init
dotnet user-secrets set Parameters:seed-product-count 25
dotnet run
```

**Why a parameter resource and not just configuration:** `AddParameter`
creates a resource the AppHost tracks like any other -- it shows up in
`manifest.json` as its own `parameter.v0` entry, it can be marked `secret: true`
(see `postgres-password` and `cache-password` in `solution/AppHost/manifest.json`,
which Aspire generates automatically for the Postgres/Redis containers'
credentials), and its value flows through `WithEnvironment("SeedProductCount", seedProductCount)`
into CatalogApi the same way a connection string does. This is one
mechanism for both "a value I want to tune without redeploying" (seed
count) and "a secret I never want in source control" (a real API key),
unified under one API.

**Your Task:** After changing the parameter and adding `.WithReplicas(2)`,
run the AppHost, open the dashboard, and confirm:
1. The resources list (or resource graph) shows **two** independent
   `catalogapi` entries, each with its own console log stream and its own
   process.
2. Refresh Storefront's `/` endpoint several times and check the trace
   list -- requests should land on either replica (Aspire's injected
   service discovery load-balances across all endpoints registered for a
   resource name).
3. If you set `seed-product-count` to `25` via user-secrets, a **fresh**
   Postgres volume seeds 25 products, not 10. (If you'd already run the app
   before with the default, you'll need to remove the `WithDataVolume()`
   volume, or run `docker volume rm`, to see this -- seeding only happens
   when the table is empty.)

**Questions to think about:**
1. Why does `WithReplicas(2)` only need to be applied to `catalogapi`'s
   builder chain, and not to `storefront`'s `WithReference(catalogApi)`
   call, for load balancing to work?
2. What's the difference between `WithEnvironment("SeedProductCount", seedProductCount)`
   (a parameter resource) and `WithEnvironment("SeedProductCount", "10")`
   (a plain string)? When would you prefer each?

## A.7 -- Testing with Aspire

Open `tests/SmokeTests.cs`:

```csharp
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.CloudNative.Tests;

public class SmokeTests
{
    [Fact]
    public async Task Can_start_app_host_and_reach_storefront_through_catalogapi()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync("storefront", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));

        using var client = app.CreateHttpClient("storefront");
        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
    }
}
```

**Why this test is different from anything earlier in this repo:**
`DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>()`
builds and runs the **real** AppHost -- real Postgres and Redis containers,
real CatalogApi and Storefront processes, real HTTP calls between them --
inside the test process's lifetime. `WaitForResourceAsync(..., KnownResourceStates.Running)`
is necessary because none of this starts instantly: pulling container
images and running EF Core's `EnsureCreatedAsync` takes real wall-clock
time, so asserting immediately after `StartAsync()` would be flaky.
`app.CreateHttpClient("storefront")` gets you an `HttpClient` already
pointed at wherever Storefront actually landed -- the test never hardcodes
a port either.

This test needs Docker to actually run (it isn't runnable in the sandbox
this exercise was authored in -- confirm it builds, and run it for real on
your own machine once Docker is available).

**Your Task:** Add three more tests to `tests/` (a new file is fine, e.g.
`CatalogApiTests.cs`, or extend `SmokeTests.cs`):

1. **Seeded count**: call `GET /products` on `catalogapi` directly (`app.CreateHttpClient("catalogapi")`)
   and assert the returned list has exactly as many items as the default
   `seed-product-count` parameter (10).
2. **Output cache actually caches**: call `GET /products` twice in a row
   and assert something that can only be true if the second response came
   from Redis instead of Postgres -- either a response header the output
   caching middleware adds on a cache hit, or (more crudely) that both
   responses are byte-identical even though nothing prevents the seed data
   from changing between calls in a buggier implementation.
3. **Both health endpoints**: call `GET /health` and `GET /alive` and
   assert both succeed and report healthy, once the resource is Running.

A real, working version of all three lives in `tests/CatalogApiTests.cs` in
this exercise's completed workspace -- write your own first, then compare.

**Questions to think about:**
1. Why does this test project reference `solution/AppHost/AppHost.Solution.csproj`
   specifically, rather than your in-progress workspace `AppHost.csproj`?
2. `WaitForResourceAsync` takes a `KnownResourceStates` value. What would
   you wait for differently if you wanted to test that a *misconfigured*
   resource fails to start, instead of testing the happy path?

---

# Part B: Deploying to Azure with `azd`

Nothing in Part B was run against a real Azure subscription while writing
this exercise -- there was no `az`/`azd` CLI available in that environment.
Every step here is accurate to how Aspire + `azd` work, and B.2 is fully
hands-on with zero Azure access required, but B.3 onward describes what to
expect rather than pasting real captured output, because that output
depends on your `azd` version, your subscription, and your region. Treat
this as a careful guide for **your own machine**, not a transcript.

## B.1 -- What `azd` Actually Does with an Aspire AppHost

`azd` does not know anything about C#, Postgres, or Redis. What it knows
is: run this AppHost with `--publisher manifest`, read the JSON it prints,
and turn that JSON into Bicep. You already have the real output of that
first step sitting in this repository: `solution/AppHost/manifest.json`.

Open it again and look at the resource `"type"` values:
- `"parameter.v0"` (`seed-product-count`, `postgres-password`, `cache-password`)
  becomes a Bicep parameter, generated into the deployment or pulled from
  Key Vault when marked `secret: true`.
- `"container.v0"` (`postgres`, `cache`) becomes a Container App running
  that exact image (`docker.io/library/postgres:17.6`, `docker.io/library/redis:8.2`)
  inside the same Container Apps environment as everything else -- **not**
  a managed Azure Database or Azure Cache resource. That distinction
  matters a lot for cost; see B.5.
- `"value.v0"` (`catalogdb`) isn't a running thing at all -- it's a
  computed connection string built from another resource's bindings, with
  nothing to provision.
- `"project.v0"` (`catalogapi`, `storefront`) becomes its own Container
  App, built from a container image `azd` builds from your project on the
  fly, with the `env` block you can already read in the manifest wired in
  as that Container App's environment variables.

This is why `Aspire.Hosting.Azure.AppContainers` is referenced in
`AppHost.csproj` (guarded with a comment explaining it's Part-B-only): it
declares the Azure Container Apps environment as an explicit resource type
the manifest/publisher understands, so `azd` knows to target ACA rather
than some other compute option.

## B.2 -- Hands-On: Regenerate and Read the Manifest Yourself

This step needs no Azure access at all -- it's the same command already
used to produce the `manifest.json` checked into this repo.

**Your Task:**

```bash
cd 06-CloudNative/Aspire
dotnet run --project solution/AppHost -- --publisher manifest --output-path manifest.json
```

This runs the AppHost binary itself in a special mode that stops before
starting any real resource -- no Docker required for this specific command,
even though running the AppHost normally does need it.

Open the regenerated file and, without looking back at this document, list:
1. Every resource of type `container.v0`, and what image each one runs.
2. Every resource of type `project.v0`, and which `.csproj` path each
   points at.
3. Every resource of type `parameter.v0`, and which ones are marked
   `"secret": true`.
4. The two environment variable names under `storefront` that prove
   service discovery survives into the manifest (you already met these in
   A.4).

**Why this exercise matters:** the manifest is the actual contract between
your AppHost code and everything `azd`/`aspire publish` does next. If a
resource is missing from it, or a `WithReference` you expected doesn't show
up as an `env` entry, that's a bug in the AppHost, not in `azd` -- and this
is how you'd find it, entirely locally, entirely for free.

## B.3 -- `azd init`

From `solution/` (the folder containing `AppHost/`, `CatalogApi/`, and
`Storefront/`):

```bash
azd init
```

Expect `azd` to detect the Aspire AppHost automatically (it looks for a
project with `IsAspireHost` set, same as `AppHost.csproj` here) and offer
to scaffold an `azure.yaml` plus an `.azure/` environment folder for you,
prompting for an environment name and an Azure region. Don't assume the
exact prompts or file contents match any particular `azd` version's
documentation exactly -- confirm against `azd`'s own `--help` and whatever
version you have installed (`azd version`).

**Your Task (on your own machine):** run `azd init`, read the generated
`azure.yaml`, and confirm it references the AppHost project. Do not run
`azd up` yet.

## B.4 -- Reviewing Bicep Before Spending Anything

**Your Task:**

```bash
azd provision --preview
```

This turns the manifest into Bicep (the same translation described in
B.1) and shows you a plan -- what resource group, what resources, roughly
what they'll cost -- without creating anything. Read through the resource
list it prints. This is the step to actually stop and read, every time,
before `azd up`; skipping it is listed as a common mistake in `README.md`
for a reason.

## B.5 -- `azd up`

```bash
azd up
```

This provisions and deploys in one command. Expect it to create, at
minimum:
- A **resource group** scoping everything else.
- A **user-assigned managed identity**, used by the Container Apps
  environment and Container Registry so nothing needs a stored credential.
- An **Azure Container Registry**, which `azd` pushes the images it builds
  for `catalogapi` and `storefront` into.
- A **Log Analytics workspace**, backing both the Container Apps
  environment's logs and (if configured) Application Insights -- this is
  where the OpenTelemetry data from `ServiceDefaults` ends up once
  `OTEL_EXPORTER_OTLP_ENDPOINT` points at Azure Monitor instead of the
  local dashboard.
- A **Container Apps environment**, the shared network/compute boundary
  all the Container Apps below run inside.
- **One Container App per `project.v0` resource** -- `catalogapi` and
  `storefront` -- each configured with the environment variables from the
  manifest (connection strings, service discovery entries, the parameter
  value).
- **Postgres and Redis as containers inside the same Container Apps
  environment** -- because that's what `container.v0` in the manifest maps
  to, not a managed database service.

### COST WARNING

Keep Postgres and Redis running as plain containers inside the Container
Apps environment for this exercise, exactly as the manifest already
describes them. Container Apps are consumption-priced and this whole setup
should cost very little to run for a short exercise and then tear down.

**Swapping `AddPostgres`/`AddRedis` for managed Azure Database for
PostgreSQL / Azure Cache for Redis is a real, ongoing cost** -- those are
provisioned, billed resources, not consumption-priced containers, and
`azd down` does not always clean up every trace of a managed database
instance as tidily as a container. If you want to try it (Aspire supports
this via `AddAzurePostgresFlexibleServer()` / `AddAzureRedis()` on the
AppHost side), treat it as an optional, deliberate advanced step you
opt into with eyes open about billing -- never the default path for
working through this exercise.

## B.6 -- Cleanup

```bash
azd down --purge
```

`--purge` matters for resources Azure soft-deletes by default (Key Vault
being the classic example) -- without it, a soft-deleted resource can keep
counting against quota or even billing in some cases even though `azd down`
"removed" it.

**Your Task:** After `azd down --purge` finishes, verify the resource group
is actually gone, don't just trust the command's exit code:

```bash
az group show --name <your-resource-group-name>
```

This should fail (a 404-equivalent "resource group could not be found"
error), not return a JSON blob. If it still returns something, the
resource group is still deleting (Azure deletions are asynchronous) or
didn't fully delete -- check the Azure Portal's Activity Log for the
resource group before assuming everything is clean.

## B.7 -- CI/CD with `azd pipeline config`

`azd pipeline config` wires the same `azd up`/`azd down` flow into a CI
provider (GitHub Actions or Azure Pipelines) instead of your terminal: it
creates a service principal (or federated credential) Azure trusts, stores
the environment's configuration as CI secrets/variables, and generates a
workflow file that runs `azd deploy` (or the full `azd up`) on push. The
underlying mechanism doesn't change at all -- it's still "run the AppHost
with `--publisher manifest`, turn that into Bicep, provision, deploy" --
only the trigger moves from your keyboard to a CI event. This exercise
does not wire this up (there is no CI pipeline in this repository to attach
it to), but understanding that `azd pipeline config` is automating exactly
the commands you already ran by hand in B.3-B.6, not something categorically
different, is the useful takeaway.

---

## Reflection Questions

1. What does Aspire's AppHost actually *run* -- is it a container
   orchestrator like Kubernetes, a process supervisor, both, or neither?
2. Name three things `AddNpgsqlDbContext<T>("catalogdb")` gives you that
   `services.AddDbContext<T>(o => o.UseNpgsql(connectionString))` does not,
   for the same underlying database.
3. Trace through exactly what changes -- in code, in configuration, in
   infrastructure -- between running this app locally with `dotnet run`
   and running it in Azure after `azd up`. What stays *identical*?
4. Why does the manifest represent `catalogdb` as `"type": "value.v0"`
   instead of a `container.v0` or `project.v0` like everything else it
   depends on?
5. If you had to explain to a teammate why `.WithReplicas(2)` on
   `catalogapi` didn't require any code change in `Storefront/Program.cs`,
   what would you say is doing the actual load-balancing work?
6. Why is reviewing `azd provision --preview` output described as
   non-optional in this exercise, when `azd up` would happily skip straight
   to provisioning if you let it?
