# Exercise: Serverless Azure Functions with Entra ID, Managed Identity, and Bicep

## Overview

You're going to build a small, secured serverless API twice: once by typing
it yourself into the empty workspace project (`FunctionApi/`), guided by
this file step by step, and once more by reading the fully-worked version
in `solution/FunctionApi/` whenever you get stuck or want to double check
something.

The API is a tiny notes service:

```
   client (client-credentials flow)
       │
       │  Bearer token (Entra ID access token)
       ▼
  Azure Function App  ──── GET  /api/health              (anonymous)
  (isolated worker)   ──── GET  /api/notes                (secured)
                       ──── POST /api/notes                (secured)
                       ──── GET  /api/me                   (secured)
                       ──── GET  /api/secure/config-check   (secured, reads Key Vault)
```

Nothing here has a database. The point of this module is the *pipeline*
around the request -- the isolated-worker host, a hand-rolled Entra ID
authentication middleware, and a managed identity that lets the Function
read a secret without ever holding a credential. Part 4 then takes the
same app to a real Azure subscription with Bicep.

## Prerequisites

- Read `solution/FunctionApi/Program.cs` and every file under
  `solution/FunctionApi/Security/`, `Notes/`, and `KeyVault/` once before
  starting. You are about to retype most of it; know what you're aiming
  for.
- .NET 9 SDK (already required by the rest of this repo).
- Parts 1-3 need **no** Azure account, no Docker, and no Azure Functions
  Core Tools -- `dotnet build`/`dotnet test` are enough to complete and
  verify all of the C# work. Core Tools + Azurite are documented for
  running the API locally as an HTTP server, but they are optional; see
  `GETTING_STARTED.md`.
- Part 4 needs an Azure subscription, the `az` CLI, and the standalone
  Bicep CLI. See `GETTING_STARTED.md` for install links -- neither is
  available in the environment this exercise was authored in, so Part 4 is
  written to be read and run on your own machine.

---

# Part 1: Build the Function API

## 1.1 -- The Isolated-Worker Host

Open `FunctionApi/Program.cs` in your workspace. It should already look
like this (the starting point, not something you need to create):

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
```

**Run it** (needs Azure Functions Core Tools -- see `GETTING_STARTED.md` if
you don't have it installed; otherwise skip running and just confirm it
builds):

```bash
dotnet build
func start
```

**Why this shape.** Azure Functions has two hosting models for .NET:
**in-process** (your code runs inside the Functions host's own process,
now on a deprecation path) and **isolated worker** (your code runs in its
own process, and the host talks to it over gRPC). This project uses
isolated worker throughout. `new HostBuilder().ConfigureFunctionsWorkerDefaults()`
is the isolated-worker equivalent of `WebApplication.CreateBuilder(args)`
-- it builds a generic .NET host, wires up the Functions-specific
middleware pipeline and JSON serialization, and gives you `IServiceCollection`
to register your own services into (you'll use this in 1.5 and Part 2).

There is a second, newer package
(`Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`) that lets
an isolated-worker Function use `HttpContext` and, in principle, ASP.NET
Core's `app.UseAuthentication()`/`[Authorize]` pipeline. This project
deliberately does **not** use it -- see Part 2.1 for why.

**Questions to think about:**
1. What actually calls your `[Function]` methods -- is it the .NET runtime,
   or something else entirely?
2. If isolated worker runs your code in a separate process from the
   Functions host, how do triggers (an HTTP request arriving) and bindings
   (a response going out) cross that process boundary?

## 1.2 -- Your First Function

**Your Task:** Create `FunctionApi/Functions/HealthFunction.cs` (it may
already exist as a starting stub -- check first):

```csharp
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace FunctionApi.Functions;

public class HealthFunction
{
    [Function("Health")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString("Healthy");
        return response;
    }
}
```

**Key concepts:**
- `[Function("Health")]` is the trigger's *logical* name (shows up in logs,
  the dashboard, `func start`'s console output) -- it does not have to
  match the class or route name, though it usually should for sanity.
- `HttpRequestData`/`HttpResponseData` are the isolated-worker model's
  request/response types. They are **not** ASP.NET Core's `HttpRequest`/
  `HttpResponse` -- no `HttpContext`, no `Request.Query`, no `[FromBody]`.
  You read headers via `req.Headers`, write a body via
  `response.WriteString`/`WriteAsJsonAsync`, and set status via
  `req.CreateResponse(HttpStatusCode...)`.
- `Route = "health"` is appended to the Functions host's fixed prefix,
  `/api/`, so this responds at `GET /api/health`.

**The `AuthorizationLevel.Anonymous` gotcha -- read this carefully.**
Every single HTTP-triggered function in this project, including the fully
secured ones you'll build in Part 2, uses `AuthorizationLevel.Anonymous`.
That is not a mistake, and it does not mean "no authentication required."

`AuthorizationLevel` controls an entirely different, older Azure Functions
mechanism: the **function key** -- a shared secret Azure can require in a
query string (`?code=...`) or an `x-functions-key` header before it will
even invoke your code, independent of anything your code does.
`AuthorizationLevel.Anonymous` means "don't require a function key."
`AuthorizationLevel.Function` means "require a valid function key."
Neither one has any idea what a JWT, a bearer token, or Entra ID is.

This project's real security gate is the Entra ID bearer-token check you
will build in Part 2 -- a custom `IFunctionsWorkerMiddleware` plus an
explicit `context.GetUser()` check inside each function that needs it.
Every function stays `Anonymous` at the function-key layer specifically
*because* the Entra ID middleware is doing the actual job; layering
function keys on top would just be a second, unrelated secret to manage
for no additional real security here.

**Questions to think about:**
1. If you set `AuthorizationLevel.Function` on `HealthFunction`, would a
   caller with a perfectly valid Entra ID token but no function key
   succeed or fail? Why?
2. Why is it fine -- even correct -- for a health probe to be `Anonymous`
   at both layers?

## 1.3 -- Why `AzureWebJobsStorage` Exists (Even With "No Database")

Open `FunctionApi/local.settings.json.example`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

**Your Task:** Copy this to `local.settings.json` (gitignored -- see
`GETTING_STARTED.md` 1.1) in your workspace `FunctionApi/` folder.

**This looks like a contradiction.** This whole module's pitch is "no
database, just the pipeline" -- so why does even the empty starting
project require a storage connection string?

Because `AzureWebJobsStorage` has nothing to do with *your* application
data. It's runtime plumbing the Functions host itself needs, regardless
of what your functions do:
- **Host lease coordination.** On a Consumption plan, multiple instances
  of your Function App can run simultaneously. The host uses blob leases
  in this storage account to coordinate which instance "owns" certain
  singleton behaviors (this matters far more for timer/queue triggers than
  the HTTP triggers in this project, but the requirement is unconditional
  -- the host demands this setting exists before it will even start).
- **Trigger bookkeeping.** Queue, blob, and timer triggers checkpoint their
  progress here. HTTP triggers, like every one in this project, don't use
  this for their own logic -- but the host still needs the account
  configured to start at all.

`UseDevelopmentStorage=true` points at **Azurite**, a local storage
emulator (documented in `GETTING_STARTED.md`; not installed in the sandbox
this module was authored in). It never touches a real Azure resource for
local development. When deployed (Part 4), this becomes a real Storage
Account -- see `infra/modules/storage.bicep`, which exists for exactly
this reason and nothing else.

So: "no database" refers to application data -- notes, in this project's
case, deliberately kept in memory (1.4). `AzureWebJobsStorage` is a
completely different concern, one layer down, that every Function App
needs regardless of what it stores.

**Questions to think about:**
1. If `HealthFunction` never reads or writes anything, why does starting
   it locally still fail without a valid `AzureWebJobsStorage` value or a
   running Azurite instance?
2. What's the actual difference in Azure between the Storage Account this
   module provisions and a hypothetical "notes database" this module
   deliberately doesn't have?

## 1.4 -- The Notes Store: What "No Database" Buys and Costs

**Your Task:** Create `FunctionApi/Notes/Note.cs`:

```csharp
namespace FunctionApi.Notes;

public record Note(int Id, string Text, string CreatedBy, DateTimeOffset CreatedAt);
```

Create `FunctionApi/Notes/INotesStore.cs`:

```csharp
namespace FunctionApi.Notes;

public interface INotesStore
{
    IReadOnlyList<Note> GetAll();
    Note Add(string text, string createdBy);
}
```

Create `FunctionApi/Notes/InMemoryNotesStore.cs`:

```csharp
using System.Collections.Concurrent;

namespace FunctionApi.Notes;

public class InMemoryNotesStore : INotesStore
{
    private readonly ConcurrentQueue<Note> _notes = new();
    private int _nextId;

    public IReadOnlyList<Note> GetAll() => _notes.ToArray();

    public Note Add(string text, string createdBy)
    {
        var note = new Note(Interlocked.Increment(ref _nextId), text, createdBy, DateTimeOffset.UtcNow);
        _notes.Enqueue(note);
        return note;
    }
}
```

**What this buys you:** zero infrastructure, zero connection strings,
zero migrations, thread-safe out of the box (`ConcurrentQueue` +
`Interlocked.Increment`), and a store you can unit test with no fakes or
containers (see `tests/InMemoryNotesStoreTests.cs` for exactly that).

**What this costs you, and why it's deliberate here:** a Consumption-plan
Function App can run **any number of instances** of your code
simultaneously, each in its own process, each with its own copy of
`InMemoryNotesStore`. A `POST /notes` handled by instance A is invisible to
a `GET /notes` handled by instance B. State also resets completely on
every cold start, redeploy, or scale-in/scale-out event. None of this
matters for what this module teaches (the auth pipeline), and pretending
otherwise with a real database would just add unrelated infrastructure to
maintain. It would matter enormously for a real notes product -- which is
exactly why the code comment on `InMemoryNotesStore` calls this out
explicitly rather than leaving it as a silent limitation.

**Questions to think about:**
1. If you deployed this today and hammered `POST /notes` with concurrent
   requests while the Function App was scaling out, what's the worst-case
   number of notes a subsequent `GET /notes` might report seeing, compared
   to how many you sent?
2. What would be the minimum change to make note storage consistent across
   instances, and why doesn't this module make that change?

## 1.5 -- Wiring Notes Into the Host

**Your Task:** Update `FunctionApi/Program.cs` to register `INotesStore`:

```csharp
using FunctionApi.Notes;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Singleton: state needs to survive across invocations on the same
        // instance (see InMemoryNotesStore's comment for what that does and
        // doesn't guarantee on a Consumption plan).
        services.AddSingleton<INotesStore, InMemoryNotesStore>();
    })
    .Build();

host.Run();
```

**Your Task:** Create `FunctionApi/Functions/NotesFunctions.cs` with an
**unsecured** version for now (you'll add the auth check in Part 2.5):

```csharp
using System.Net;
using FunctionApi.Notes;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace FunctionApi.Functions;

public class NotesFunctions(INotesStore notesStore)
{
    [Function("GetNotes")]
    public async Task<HttpResponseData> GetNotes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(notesStore.GetAll());
        return response;
    }

    [Function("CreateNote")]
    public async Task<HttpResponseData> CreateNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notes")] HttpRequestData req)
    {
        var payload = await req.ReadFromJsonAsync<CreateNoteRequest>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.Text))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "'text' is required." });
            return badRequest;
        }

        var note = notesStore.Add(payload.Text, "anonymous");
        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(note);
        return response;
    }

    private record CreateNoteRequest(string Text);
}
```

**Your Task:** Run `dotnet build`, then (if you have Core Tools + Azurite
running) `func start` and exercise both endpoints with `curl`:

```bash
curl http://localhost:7071/api/notes
curl -X POST http://localhost:7071/api/notes -H "Content-Type: application/json" -d "{\"text\":\"hello\"}"
curl http://localhost:7071/api/notes
```

Confirm the second `GET` shows the note you just created.

You'll come back to `NotesFunctions.cs` in Part 2.5 to add the real auth
check and pull the caller's identity out of the validated token instead of
hardcoding `"anonymous"`.

---

# Part 2: Secure it with Entra ID

## 2.1 -- Why `[Authorize]` Doesn't Just Work Here

If you've secured an ASP.NET Core API before, your instinct is
`app.UseAuthentication()`, `.AddAuthentication().AddJwtBearer(...)`, and
`[Authorize]` on the endpoint. None of that is available to the classic
isolated-worker host this project uses (`ConfigureFunctionsWorkerDefaults()`),
because that pipeline never builds an ASP.NET Core `WebApplication` --
there's no `IApplicationBuilder`, no authentication middleware pipeline,
and `HttpRequestData`/`HttpResponseData` are not `HttpContext`.

Microsoft does publish a newer package,
`Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`, specifically
to let an isolated-worker Function opt into the real ASP.NET Core request
pipeline (`HttpContext`, and in principle `app.UseAuthentication()`/
`[Authorize]`). This project does not use it. Two honest reasons, stated
plainly rather than asserted from nowhere:

1. As of this writing, getting `[Authorize]`/JWT bearer authentication
   working cleanly through that package in an isolated-worker Function App
   requires extra glue beyond what a plain ASP.NET Core project needs --
   it is not the drop-in experience the package name suggests, and the
   amount of glue code needed to make it work reliably ends up comparable
   to writing the middleware directly.
2. Writing the middleware directly (this section) is far more valuable to
   *learn* than configuring a framework feature you can't fully see inside
   of -- you will understand exactly what "validate a bearer token" means
   at the JWT level, which pays off directly in Part 2.6's debugging
   exercise.

If you want to verify this claim yourself rather than take it on faith:
search "azure functions isolated worker JWT bearer authentication
Http.AspNetCore" and read a few recent results before you start Part 2.2.
Confirming or updating a claim like this is exactly the kind of thing
worth doing rather than trusting a comment in a training repo forever.

**Questions to think about:**
1. What does `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore`
   actually change about how your Function process starts, compared to
   `ConfigureFunctionsWorkerDefaults()` alone?
2. If this project used that package and `[Authorize]` worked flawlessly,
   would `EntraIdTokenValidator`'s logic (2.3-2.6) become unnecessary, or
   would it just move somewhere else?

## 2.2 -- The Shape of the Middleware

Before writing any code, look at the two seams this design uses, both
already sketched for you as interfaces you'll implement:

- `IEntraIdConfigurationProvider` -- fetches (and caches) Entra ID's
  current issuer and signing keys.
- `ITokenValidator` -- takes a raw bearer token string and returns a
  `TokenValidationOutcome` (either a validated `ClaimsPrincipal`, or a
  failure reason).

**Your Task:** Create `FunctionApi/Security/ITokenValidator.cs`:

```csharp
using System.Security.Claims;

namespace FunctionApi.Security;

public interface ITokenValidator
{
    Task<TokenValidationOutcome> ValidateAsync(string bearerToken, CancellationToken cancellationToken);
}

public record TokenValidationOutcome
{
    public required bool IsValid { get; init; }
    public ClaimsPrincipal? Principal { get; init; }
    public string? FailureReason { get; init; }

    public static TokenValidationOutcome Success(ClaimsPrincipal principal) =>
        new() { IsValid = true, Principal = principal };

    public static TokenValidationOutcome Failure(string reason) =>
        new() { IsValid = false, FailureReason = reason };
}
```

Create `FunctionApi/Security/IEntraIdConfigurationProvider.cs`:

```csharp
using Microsoft.IdentityModel.Tokens;

namespace FunctionApi.Security;

public interface IEntraIdConfigurationProvider
{
    Task<EntraIdSigningConfiguration> GetConfigurationAsync(CancellationToken cancellationToken);
}

public record EntraIdSigningConfiguration(string Issuer, IReadOnlyList<SecurityKey> SigningKeys);
```

**Why split it this way:** the whole point of this seam is that
`EntraIdTokenValidator`'s validation logic (issuer, audience, signature,
lifetime, role) is completely decoupled from *how* the signing keys were
obtained. In production, `EntraIdConfigurationProvider` fetches them from
Entra ID over the network (2.4). In tests, `TestTokenFactory` supplies a
fixed, locally-generated RSA key instead -- the exact same validator code
runs with zero network access and zero real tenant. That's what makes the
tests in `tests/EntraIdTokenValidatorTests.cs` genuinely fast, free, and
runnable in a sandbox with no Azure access at all.

## 2.3 -- Options: What's Secret, What Isn't

**Your Task:** Create `FunctionApi/Security/EntraIdOptions.cs`:

```csharp
namespace FunctionApi.Security;

public class EntraIdOptions
{
    public required string TenantId { get; set; }
    public required string Audience { get; set; }
    public required string RequiredAppRole { get; set; }

    public string Authority => $"https://login.microsoftonline.com/{TenantId}/v2.0";
    public string MetadataAddress => $"{Authority}/.well-known/openid-configuration";
}
```

**Notice what's absent: no client secret, no API key, nothing sensitive.**
A tenant ID, an audience (`api://<app-id>`), and a role name are all
public information about an App Registration -- knowing them lets you
*ask* for a token, not forge one. This is exactly why
`local.settings.json.example` can be committed with real-looking values
and needs no redaction, and why the Bicep in Part 4 passes these as plain
(non-`@secure()`) app settings.

**Questions to think about:**
1. If someone found your `Audience` and `TenantId` values in a public
   GitHub repo, what could they actually do with that information alone?
2. Why is `RequiredAppRole` configuration at all, rather than a hardcoded
   string in `EntraIdTokenValidator`?

## 2.4 -- Fetching Entra ID's Signing Keys

**Your Task:** Create `FunctionApi/Security/EntraIdConfigurationProvider.cs`:

```csharp
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FunctionApi.Security;

public class EntraIdConfigurationProvider : IEntraIdConfigurationProvider
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

    public EntraIdConfigurationProvider(IOptions<EntraIdOptions> options)
    {
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            options.Value.MetadataAddress,
            new OpenIdConnectConfigurationRetriever());
    }

    public async Task<EntraIdSigningConfiguration> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
        return new EntraIdSigningConfiguration(configuration.Issuer, configuration.SigningKeys.ToList());
    }
}
```

**What's actually happening:** `MetadataAddress` points at Entra ID's
OpenID Connect discovery document
(`https://login.microsoftonline.com/{tenant}/v2.0/.well-known/openid-configuration`),
which itself points at a JWKS (JSON Web Key Set) endpoint containing
Entra ID's current public signing keys. `ConfigurationManager<T>` fetches
and caches both automatically -- by default it refreshes roughly every 24
hours, and sooner if a validation ever fails because of an unrecognized
key ID (which is exactly what happens when Entra ID rotates its signing
keys, something you have no control over and must always be ready for).
This is the actual mechanism platforms like Easy Auth or
Microsoft.Identity.Web hide behind a one-line setup call -- here it's
explicit so you can see every step.

## 2.5 -- The Validator, and Wiring It Into a Middleware

**Your Task:** Create `FunctionApi/Security/EntraIdTokenValidator.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FunctionApi.Security;

public class EntraIdTokenValidator(
    IEntraIdConfigurationProvider configurationProvider,
    IOptions<EntraIdOptions> options) : ITokenValidator
{
    private const string RoleClaimType = "roles";

    public async Task<TokenValidationOutcome> ValidateAsync(string bearerToken, CancellationToken cancellationToken)
    {
        var signingConfiguration = await configurationProvider.GetConfigurationAsync(cancellationToken);
        var entraOptions = options.Value;

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = signingConfiguration.Issuer,
            IssuerSigningKeys = signingConfiguration.SigningKeys,
            ValidAudience = entraOptions.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var handler = new JwtSecurityTokenHandler();

        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(bearerToken, validationParameters, out _);
        }
        catch (SecurityTokenException ex)
        {
            return TokenValidationOutcome.Failure($"Token failed validation: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return TokenValidationOutcome.Failure($"Token is malformed: {ex.Message}");
        }

        var roles = principal.FindAll(RoleClaimType).Select(c => c.Value).ToArray();
        if (!roles.Contains(entraOptions.RequiredAppRole))
        {
            return TokenValidationOutcome.Failure(
                $"Token is valid but missing required app role '{entraOptions.RequiredAppRole}'. " +
                $"Roles present: [{string.Join(", ", roles)}]");
        }

        return TokenValidationOutcome.Success(principal);
    }
}
```

Notice what each `TokenValidationParameters` flag catches:
- `ValidateIssuerSigningKey` + `IssuerSigningKeys` -- the signature. Was
  this token actually signed by a key Entra ID controls?
- `ValidateIssuer` + `ValidIssuer` -- was it issued by *your* tenant, not
  someone else's?
- `ValidateAudience` + `ValidAudience` -- was it issued *for your API*, not
  some other application entirely? A token can be perfectly validly signed
  and issued by the right tenant while being meant for a completely
  different app -- audience is what catches that.
- `ValidateLifetime` -- has it expired (or not started yet)?

And one thing Entra ID does **not** check for you at all: whether *this
specific caller* is allowed to use *this specific API*. That's the
`roles.Contains(entraOptions.RequiredAppRole)` check at the bottom --
without it, any client that can get a token for your tenant (for any
app registration in it) could call your API.

**Your Task:** Create `FunctionApi/Security/FunctionContextExtensions.cs`:

```csharp
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;

namespace FunctionApi.Security;

public static class FunctionContextExtensions
{
    private const string UserItemKey = "EntraIdUser";

    internal static void SetUser(this FunctionContext context, ClaimsPrincipal principal) =>
        context.Items[UserItemKey] = principal;

    public static ClaimsPrincipal? GetUser(this FunctionContext context) =>
        context.Items.TryGetValue(UserItemKey, out var value) ? value as ClaimsPrincipal : null;
}
```

Create `FunctionApi/Security/EntraIdAuthenticationMiddleware.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace FunctionApi.Security;

public class EntraIdAuthenticationMiddleware(
    ITokenValidator tokenValidator,
    ILogger<EntraIdAuthenticationMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var requestData = await context.GetHttpRequestDataAsync();

        if (requestData is not null && TryGetBearerToken(requestData, out var token))
        {
            var outcome = await tokenValidator.ValidateAsync(token, context.CancellationToken);

            if (outcome.IsValid)
            {
                context.SetUser(outcome.Principal!);
            }
            else
            {
                logger.LogWarning("Bearer token rejected: {Reason}", outcome.FailureReason);
            }
        }

        await next(context);
    }

    private static bool TryGetBearerToken(HttpRequestData requestData, out string token)
    {
        token = string.Empty;

        if (!requestData.Headers.TryGetValues("Authorization", out var values))
        {
            return false;
        }

        var header = values.FirstOrDefault();
        const string prefix = "Bearer ";

        if (header is null || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = header[prefix.Length..].Trim();
        return token.Length > 0;
    }
}
```

**A deliberate design decision, worth noticing:** this middleware never
rejects a request itself. No token, an expired token, a token for the
wrong app -- all of these just mean `context.GetUser()` returns `null`
afterward, and the middleware calls `next(context)` regardless. **Every
function that needs auth is responsible for checking `context.GetUser()`
itself** and returning `401 Unauthorized` if it's `null` -- exactly the
pattern already used by `HealthFunction` staying anonymous while
`NotesFunctions` will check for a user (below). This keeps "is a token
present and valid" (the middleware's job, applied uniformly) separate from
"does this specific endpoint require one" (each function's own, visible
decision) -- notice this is the same explicit-check pattern
`AuthorizationLevel.Anonymous` forced you to reason about in 1.2, just one
layer up.

**Note the important namespace:** `IFunctionsWorkerMiddleware` lives in
`Microsoft.Azure.Functions.Worker.Middleware`. If you see
`Microsoft.Azure.Functions.Worker.Pipeline` referenced anywhere (an older
blog post, an AI-generated snippet), that namespace does not exist in the
current package -- it will not compile.

**Your Task:** Wire the middleware and validator into `Program.cs`:

```csharp
using FunctionApi.Notes;
using FunctionApi.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<EntraIdAuthenticationMiddleware>();
    })
    .ConfigureServices(services =>
    {
        services
            .AddOptions<EntraIdOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                configuration.GetSection("EntraId").Bind(options));

        services.AddSingleton<IEntraIdConfigurationProvider, EntraIdConfigurationProvider>();
        services.AddSingleton<ITokenValidator, EntraIdTokenValidator>();
        services.AddSingleton<INotesStore, InMemoryNotesStore>();
    })
    .Build();

host.Run();
```

Add the required packages to `FunctionApi.csproj` (already listed as
`<!-- TODO Part 2 -->` comments -- uncomment/replace them):

```xml
<PackageReference Include="Microsoft.IdentityModel.Protocols.OpenIdConnect" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" />
```

Now update `NotesFunctions.cs` to actually enforce the check, and use the
caller's real identity instead of the `"anonymous"` placeholder from 1.5:

```csharp
using System.Net;
using FunctionApi.Notes;
using FunctionApi.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace FunctionApi.Functions;

public class NotesFunctions(INotesStore notesStore)
{
    [Function("GetNotes")]
    public async Task<HttpResponseData> GetNotes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequestData req,
        FunctionContext context)
    {
        if (context.GetUser() is null)
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(notesStore.GetAll());
        return response;
    }

    [Function("CreateNote")]
    public async Task<HttpResponseData> CreateNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notes")] HttpRequestData req,
        FunctionContext context)
    {
        var user = context.GetUser();
        if (user is null)
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var payload = await req.ReadFromJsonAsync<CreateNoteRequest>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.Text))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "'text' is required." });
            return badRequest;
        }

        var callerId = user.FindFirst("appid")?.Value ?? user.FindFirst("azp")?.Value ?? "unknown-caller";
        var note = notesStore.Add(payload.Text, callerId);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(note);
        return response;
    }

    private record CreateNoteRequest(string Text);
}
```

**Your Task:** Create `FunctionApi/Functions/MeFunction.cs`, which just
echoes back whatever the middleware validated -- the clearest way to see
exactly what claims a token carries once you have one (2.7):

```csharp
using System.Net;
using FunctionApi.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace FunctionApi.Functions;

public class MeFunction
{
    [Function("Me")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequestData req,
        FunctionContext context)
    {
        if (context.GetUser() is not { } user)
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        var claims = user.Claims.Select(c => new { c.Type, c.Value });
        await response.WriteAsJsonAsync(new { claims });
        return response;
    }
}
```

`dotnet build` before moving on.

## 2.6 -- Discover the `MapInboundClaims` Gotcha Yourself

**Your Task:** Before reading any further, write (or reason through) this
test against the `EntraIdTokenValidator` you just built:

1. Create a token whose payload genuinely contains `"roles": ["Notes.Access"]`
   (you can adapt `tests/TestTokenFactory.cs`'s `CreateToken` -- it's
   already built for exactly this).
2. Configure the validator with `RequiredAppRole = "Notes.Access"`.
3. Call `ValidateAsync` and check `outcome.IsValid`.

Run it. **If your `EntraIdTokenValidator.cs` looks exactly like the code
block in 2.5, this fails** -- `outcome.IsValid` is `false`, and
`outcome.FailureReason` says something like `"Token is valid but missing
required app role 'Notes.Access'. Roles present: []"` -- even though the
token you built plainly has that role in it.

**Stop and actually investigate this before reading on.** Add a
`Console.WriteLine` (or a debugger breakpoint) right after
`handler.ValidateToken(...)` and print every claim on `principal.Claims`
-- `c.Type` and `c.Value` for each one. Look for anything that resembles
a role claim. What type is it actually using?

You should find a claim with `c.Value == "Notes.Access"` but
`c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"`
-- not `"roles"`.

**The explanation:** `JwtSecurityTokenHandler` has, for many years and by
default, remapped a set of short, standard JWT claim names to legacy
WS-Federation claim URIs when it builds the `ClaimsPrincipal` --
`"roles"` becomes that long `.../claims/role` URI,
`"sub"` becomes `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`,
and so on. This happens silently, inside `ValidateToken`, after signature
and lifetime validation already succeeded -- the token itself is
completely valid, and the role genuinely is in the raw payload; it's the
*handler* quietly renaming the claim type on the way out.

**The fix:**

```csharp
var handler = new JwtSecurityTokenHandler
{
    MapInboundClaims = false
};
```

**Your Task:** Add this to `EntraIdTokenValidator.cs`, re-run your test
from the top of this section, and confirm it now passes.

This is exactly the fix already present (with a full explanatory comment)
in `solution/FunctionApi/Security/EntraIdTokenValidator.cs`, and it's
covered by a real regression test:
`tests/EntraIdTokenValidatorTests.cs`'s
`Token_missing_the_required_app_role_is_rejected` test would falsely start
passing (for the wrong reason) if `MapInboundClaims = false` were ever
removed and a role check elsewhere used the remapped claim type instead --
run `dotnet test` now and confirm all 10 tests pass with the fix in place.

**Why this is worth an entire section:** every Entra ID integration guide
mentions this somewhere, usually as a single line you can easily skip past
without understanding why it matters. Finding it by tracing a specific,
reproducible failure -- a token with the role, rejected for "missing" that
exact role -- is the only way to actually remember it. If you ever debug a
"missing role" error against a real Entra ID token in the future and the
token plainly has the role when you decode it at jwt.ms, this is the first
thing to check.

## 2.7 -- Two App Registrations and the Client-Credentials Flow

Read `infra/modules/appRegistrations.bicep` now (you don't need to deploy
it -- Part 4 covers that). It provisions two Entra ID **App
Registrations**:

- **API app** (`netlearn-functions-api`) -- exposes an **app role** of
  `allowedMemberTypes: ["Application"]` and `value: "Notes.Access"`. The
  `"Application"` member type (as opposed to `"User"`) is what makes this
  role assignable to another *application's* service principal, not a
  human user -- the whole point, since nothing here involves a user
  signing in.
- **Client app** (`netlearn-functions-client`) -- gets a client secret, and
  its service principal is assigned to the API app's role via a direct
  Microsoft Graph call (`appRoleAssignedTo` -- there's no plain `az ad`
  subcommand for this specific grant).

**Why client-credentials flow specifically:** this is a
service-to-service call with no human in the loop -- no browser, no
redirect URI, no user consent screen, nothing to sign into. OAuth 2.0's
client-credentials grant is built exactly for this: a client
authenticates as *itself* (client ID + client secret, or a certificate)
directly against the token endpoint and gets back a token representing
"this application," not "this application acting on behalf of a user."
That's exactly what `appid`/`azp` in `MeFunction`'s claim dump will show
you -- an application identity, not a user identity, because there never
was a user.

## 2.8 -- Getting a Real Token to Test With

Once you have both App Registrations (from Part 4's deployment, or created
manually in the Portal for now if you want to test before Part 4), request
a token directly with `curl`:

```bash
curl -X POST "https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=<client-app-id>" \
  -d "client_secret=<client-secret>" \
  -d "scope=api://<api-app-id>/.default" \
  -d "grant_type=client_credentials"
```

Note `scope=<audience>/.default` -- for client-credentials flow, you
always request `.default`, which means "whatever app roles/permissions
this client has already been granted for that resource," not a
space-delimited list of individual scopes the way delegated (user) flows
work.

Or as an `.http` file (VS Code REST Client / Visual Studio's built-in
HTTP editor):

```http
@tenantId = <tenant-id>
@clientId = <client-app-id>
@clientSecret = <client-secret>
@apiAppId = <api-app-id>

### Get a token
POST https://login.microsoftonline.com/{{tenantId}}/oauth2/v2.0/token
Content-Type: application/x-www-form-urlencoded

client_id={{clientId}}&client_secret={{clientSecret}}&scope=api://{{apiAppId}}/.default&grant_type=client_credentials

### Call the API with it (paste the access_token from the response above)
GET https://<your-function-app>.azurewebsites.net/api/me
Authorization: Bearer <paste-token-here>
```

**Your Task:** Paste the resulting `access_token` into
[jwt.ms](https://jwt.ms) (Microsoft's own JWT decoder) and confirm you can
see `"roles": ["Notes.Access"]`, `"aud": "api://<api-app-id>"`, and
`"iss"` pointing at your tenant, in the raw, undecoded payload -- this is
your own visual confirmation of exactly what 2.6 taught you gets remapped
by the .NET handler afterward.

**Questions to think about (Part 2):**
1. What HTTP status code does a call to `/api/notes` with **no**
   `Authorization` header get, and where exactly in the code does that
   status get chosen?
2. What HTTP status code does a call with a syntactically valid but
   expired token get -- is it different from no token at all, and should
   it be?
3. If Entra ID rotated its signing keys right now, how long could it take
   before `EntraIdConfigurationProvider` notices, in the worst case, and
   what would a request look like during that window?

---

# Part 3: Managed Identity + Key Vault

## 3.1 -- The Anti-Pattern First

Imagine `local.settings.json` looked like this:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "DemoSecretValue": "sup3r-s3cr3t-do-not-commit"
  }
}
```

This is the anti-pattern this part exists to replace. Even with the file
gitignored locally, this value would have to be duplicated into every
environment's app settings by hand, rotated manually, and would appear in
plaintext in Azure Portal's configuration blade to anyone with Reader
access to the Function App. There's no audit trail of who read it, no
automatic rotation, and no single source of truth.

## 3.2 -- `ISecretReader` and `DefaultAzureCredential`

**Your Task:** Create `FunctionApi/KeyVault/ISecretReader.cs`:

```csharp
namespace FunctionApi.KeyVault;

public interface ISecretReader
{
    Task<string> GetSecretValueAsync(string secretName, CancellationToken cancellationToken);
}
```

Create `FunctionApi/KeyVault/KeyVaultOptions.cs`:

```csharp
namespace FunctionApi.KeyVault;

public class KeyVaultOptions
{
    public required string VaultUri { get; set; }
    public required string SecretName { get; set; }
}
```

Create `FunctionApi/KeyVault/KeyVaultSecretReader.cs`:

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;

namespace FunctionApi.KeyVault;

public class KeyVaultSecretReader : ISecretReader
{
    private readonly SecretClient _client;

    public KeyVaultSecretReader(IOptions<KeyVaultOptions> options)
    {
        _client = new SecretClient(new Uri(options.Value.VaultUri), new DefaultAzureCredential());
    }

    public async Task<string> GetSecretValueAsync(string secretName, CancellationToken cancellationToken)
    {
        var secret = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
        return secret.Value.Value;
    }
}
```

Add the packages to `FunctionApi.csproj`:

```xml
<PackageReference Include="Azure.Identity" />
<PackageReference Include="Azure.Security.KeyVault.Secrets" />
```

**No connection string, no access key, nowhere in configuration.**
`DefaultAzureCredential` tries a sequence of credential sources and uses
the first one that works: environment variables, a workload identity,
**managed identity** (this is the one that matters once deployed), then
falls back through Visual Studio, Azure CLI (`az login`), and Azure
PowerShell credentials for local development. It never touches a stored
secret of its own -- that's the entire point.

## 3.3 -- Managed Identity Gives an Identity; RBAC Gives Permission

Read `infra/modules/functionapp.bicep`'s `identity: { type: 'SystemAssigned' }`
and `infra/modules/keyvault.bicep`'s `secretsUserRoleAssignment` resource
together. These are two genuinely separate things, easy to conflate:

- **System-assigned managed identity** gives the Function App an identity
  in Entra ID -- a service principal Azure creates and manages for you,
  tied to the Function App's lifecycle (delete the Function App, the
  identity goes with it). This answers **"who is calling?"**
- **RBAC role assignment** (`Microsoft.Authorization/roleAssignments`,
  the "Key Vault Secrets User" built-in role, scoped to this one vault)
  answers **"what is that identity allowed to do?"** Without this role
  assignment, the Function's managed identity is a perfectly valid
  identity that Key Vault will still reject with a `403 Forbidden` --
  having an identity is not the same as having permission.

`infra/modules/keyvault.bicep` also sets `enableRbacAuthorization: true`
on the vault itself, which routes authorization through the same
`Microsoft.Authorization/roleAssignments` model used everywhere else in
Azure, instead of Key Vault's older, separate **access policies** system.
One authorization model instead of two.

## 3.4 -- Local Development Without a Key Vault Access Key

You never need a Key Vault access key, even locally: `DefaultAzureCredential`
falls back to your own `az login` session if one exists. Run:

```bash
az login
az account show
```

Then `local.settings.json`'s `KeyVault__VaultUri` can point at a real vault
(one you've deployed in Part 4, or a scratch one), and
`KeyVaultSecretReader` authenticates as *you*, using whatever RBAC roles
your own account has been granted on that vault -- no separate credential
to manage for local dev.

## 3.5 -- Wiring It Together and the Proof Endpoint

**Your Task:** Register the new services in `Program.cs`:

```csharp
services
    .AddOptions<KeyVaultOptions>()
    .Configure<IConfiguration>((options, configuration) =>
        configuration.GetSection("KeyVault").Bind(options));

services.AddSingleton<ISecretReader, KeyVaultSecretReader>();
```

Create `FunctionApi/Functions/ConfigCheckFunction.cs`:

```csharp
using System.Net;
using FunctionApi.KeyVault;
using FunctionApi.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;

namespace FunctionApi.Functions;

public class ConfigCheckFunction(ISecretReader secretReader, IOptions<KeyVaultOptions> keyVaultOptions)
{
    [Function("ConfigCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "secure/config-check")] HttpRequestData req,
        FunctionContext context)
    {
        if (context.GetUser() is null)
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var secretValue = await secretReader.GetSecretValueAsync(
            keyVaultOptions.Value.SecretName, context.CancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            message = "Read this value from Key Vault using the Function's managed identity -- no secret in config.",
            secretLength = secretValue.Length,
            secretPreview = secretValue.Length > 4 ? $"{secretValue[..2]}...{secretValue[^2..]}" : "***"
        });
        return response;
    }
}
```

Notice this still requires a valid bearer token (`context.GetUser()`),
exactly like every other secured endpoint. **Managed Identity secures the
Function's own outbound credentials to Key Vault -- it says nothing about
who is allowed to call the Function itself.** Those are two independent
security boundaries, and this endpoint deliberately enforces both.

**Your Task:** Once deployed (Part 4), call
`GET /api/secure/config-check` with a valid token and confirm
`secretPreview` shows a masked preview of the demo secret's value, proving
the Function read it with no secret anywhere in its own configuration.

---

# Part 4: Infrastructure as Code with Bicep

Nothing in this part was run against a real Azure subscription while
writing this exercise -- there is no `az`/`azd` CLI in the environment
this module was authored in. Every `.bicep` file was verified with the
standalone Bicep CLI (`bicep build`/`bicep lint`, zero errors or warnings
on every file) -- syntax and structure are solid, but no resource here has
actually been created. Treat this part as a careful, accurate guide for
**your own machine and subscription**, not a transcript of a run that
already happened. See `GETTING_STARTED.md` for the fully checkpointed
walkthrough with cost callouts at every step that can spend money; this
section focuses on understanding the Bicep itself.

## 4.1 -- Reading `main.bicep`

Open `infra/main.bicep`. It orchestrates four modules in this order:
`storage` -> `appInsights` -> `appRegistrations` -> `functionApp` ->
`keyVault`. Notice `nameSuffix` -- a required parameter with no default --
because the Storage Account and Key Vault both need **globally unique**
names across all of Azure, not just your subscription. `storage.bicep`
builds `st${nameSuffix}func` and `main.bicep` builds `kv-${nameSuffix}-func`;
pick a suffix genuinely likely to be unique (initials + a short random
string), or `az deployment group create` fails with a name-collision error
you'll only discover after everything else in the deployment already
succeeded.

## 4.2 -- The ARM/Graph Boundary

Read `infra/modules/appRegistrations.bicep`'s top comment in full. The
short version: **App Registrations and their app-role assignments live in
Microsoft Graph, not Azure Resource Manager.** There is no
`Microsoft.AzureActiveDirectory/applications` resource type you can write
as plain declarative Bicep, the way you can write
`Microsoft.Storage/storageAccounts` or `Microsoft.KeyVault/vaults`. ARM and
Graph are genuinely different control planes with different APIs,
different auth models, and different resource models.

`appRegistrations.bicep` bridges this gap with a
`Microsoft.Resources/deploymentScripts` resource -- a real ARM resource
whose entire job is to run an arbitrary Azure CLI script (`az ad app
create`, `az ad sp create`, and a direct `az rest` call to Graph's
`appRoleAssignedTo` endpoint, since there's no plain `az ad` subcommand
for that specific grant) as part of the deployment. This keeps the whole
module deployable with one `az deployment group create` command instead
of a separate manual step -- at the cost of needing its own identity
(`deploymentIdentity`, a user-assigned managed identity) and its own
permissions, which brings you to 4.5.

**Questions to think about:**
1. Why does the deployment script re-check for an existing app by display
   name (`az ad app list --display-name ...`) before creating one, instead
   of always creating a new one?
2. What would happen if you ran `az deployment group create` twice in a
   row against the same resource group with the same parameters?

## 4.3 -- The Circular Dependency: Read Before You Believe the Fix

Before reading this section's answer, look at `functionapp.bicep`'s
parameters and `keyvault.bicep`'s parameters side by side and ask
yourself: which module needs which module's output?

- `functionApp` needs the Key Vault's URI (`KeyVault__VaultUri` app
  setting), so it can talk to the right vault at runtime.
- `keyVault` needs the Function App's managed identity's `principalId`, so
  it can grant that identity the "Key Vault Secrets User" role.

If you wired this the "obvious" way -- `functionApp` takes
`keyVault.outputs.vaultUri` as a parameter, and `keyVault` takes
`functionApp.outputs.principalId` as a parameter -- Bicep would refuse to
compile it. Each module would depend on the other module's deployment
completing first, which is a genuine cycle with no valid deployment order.
Try writing it that way yourself in a scratch `.bicep` file and run
`bicep build` on it, if you want to see the actual error Bicep gives you.

**The fix**, already in `main.bicep`:

```bicep
var keyVaultName = 'kv-${nameSuffix}-func'
// Deterministic from the name, not a module output.
var keyVaultUri = 'https://${keyVaultName}${environment().suffixes.keyvaultDns}'
```

A Key Vault's URI is **entirely predictable from its name** -- it's always
`https://<vault-name><keyvault-dns-suffix>` (using
`environment().suffixes.keyvaultDns` rather than hardcoding
`.vault.azure.net` so this template keeps working, unmodified, in Azure
Government or Azure China clouds, where that suffix differs). Because the
URI can be computed from a value `main.bicep` already controls
(`keyVaultName`, built from `nameSuffix`), there's no need to read it as a
module *output* at all. `functionApp` receives `keyVaultUri` as a plain
computed string; `keyVault` still receives `functionApp.outputs.principalId`
as a real module dependency. The cycle is broken because only one
direction of the dependency was ever real -- the other direction only
*felt* necessary because both pieces of data happened to come from Bicep
modules.

**The general lesson, not just this specific fix:** when two modules seem
to need each other's outputs, check whether one of those "outputs" is
actually derivable from something you already know, rather than something
that genuinely doesn't exist until the other resource is deployed. Here,
the vault's name (hence its URI) is decided by `main.bicep` before either
module runs; only the *role assignment* genuinely requires the Function to
exist first. Separating "what can I compute now" from "what must I wait
for" is what resolves the apparent cycle.

**Your Task:** Confirm you understand this by tracing, in `main.bicep`,
the exact `module` dependency graph Bicep will infer: which modules does
`keyVault` implicitly depend on, and which does `functionApp` implicitly
depend on? (Hint: Bicep infers dependencies from any expression that
references another module's `.outputs`, not from the order modules are
written in the file.)

## 4.4 -- Verifying With Bicep CLI (No Azure Needed)

The standalone Bicep CLI needs no `az` CLI and no Azure login for these
two commands -- pure local syntax/semantic checking:

```bash
bicep build infra/main.bicep
bicep lint infra/main.bicep
bicep lint infra/modules/appRegistrations.bicep
bicep lint infra/modules/functionapp.bicep
bicep lint infra/modules/keyvault.bicep
bicep lint infra/modules/storage.bicep
bicep lint infra/modules/appinsights.bicep
```

If you don't have it installed, see
[Install Bicep](https://learn.microsoft.com/azure/azure-resource-manager/bicep/install)
-- the standalone CLI, not the `az bicep` extension, works fine for this
and needs no Azure CLI at all.

**Your Task:** Run every command above and confirm zero errors and zero
warnings, exactly as this module's author verified before writing this
exercise. `bicep build` also emits an ARM JSON template next to each file
(`main.json`, etc.) -- open `main.json` once to see what your
declarative Bicep actually compiles down to; it's worth seeing at least
once.

## 4.5 -- The One Manual Prerequisite: an Entra ID Directory Role

Read `infra/modules/appRegistrations.bicep`'s top comment once more,
specifically this part: **the `deploymentScript`'s user-assigned managed
identity (`deploymentIdentity`) must be granted the Entra ID "Application
Developer" directory role (or higher) before you run
`az deployment group create`, or the script's `az ad app create` call
fails with an authorization error.**

This is a **Microsoft Graph directory-role assignment**, not an ARM RBAC
role -- it has no Bicep resource type, and it cannot be automated inside
this template, because granting a directory role itself requires
**Global Administrator** or **Privileged Role Administrator** rights,
which is a deliberately higher bar than "can deploy infrastructure." This
is the honest boundary of "automate everything with Bicep": everything
ARM owns is fully declarative in this module; this one Graph-level grant
is not, and pretending it could be would just hide where the real
authorization decision happens.

See `GETTING_STARTED.md` Part 4 for the exact command to grant this --
run it once, before your first `az deployment group create` in this
module, by whoever in your tenant has the rights to grant directory roles.

## 4.6 -- Deploying and Cost Notes

Full checkpointed walkthrough with cost callouts at every spending step is
in `GETTING_STARTED.md`. In outline:

```bash
az login
az group create --name <rg-name> --location <location>
az deployment group create \
  --resource-group <rg-name> \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --parameters keyVaultSecretValue='whatever you want for the demo secret'
```

**This is the cheapest module in this repo to run end to end.** The
Consumption plan (`Y1`/`Dynamic` SKU, see `functionapp.bicep`) bills
per-execution with a substantial monthly free grant, and a Key Vault with
one secret and light traffic costs fractions of a cent per operation.
Compare this to `06-CloudNative/Aspire`'s Container Apps environment
(always-provisioned compute, billed even mostly idle) or
`11-*`'s Cosmos DB (provisioned or serverless RU-based billing that adds
up faster than a Consumption Function): this module, run for a short
exercise and torn down promptly, should cost you well under the price of
a cup of coffee, if anything at all.

## 4.7 -- Teardown and Verifying It's Actually Gone

```bash
az group delete --name <rg-name> --yes --no-wait
```

Then verify, don't just trust the command:

```bash
az group show --name <rg-name>
```

This should fail with a "could not be found" error, not return JSON. If it
still returns something, deletion is still in progress (Azure deletions
are asynchronous, and this module's Key Vault has `enablePurgeProtection: true`
-- see `keyvault.bicep` -- meaning the vault itself soft-deletes and won't
be purgeable for its retention period even after the resource group is
gone; this is expected and does not keep costing you anything once
soft-deleted).

**Questions to think about (Part 4):**
1. If you deployed this module twice with the same `nameSuffix` into two
   different resource groups, what would happen to the second deployment,
   and why?
2. Why does `appRegistrationScript`'s `cleanupPreference` matter
   (`'OnSuccess'` in this module), and what would change if it were
   `'Always'`?

---

## Reflection Questions

1. Trace exactly what happens, end to end, when a client with **no**
   Authorization header calls `GET /api/notes`: which layer notices, and
   with what response?
2. Why does `AuthorizationLevel.Anonymous` appear on every single
   `[HttpTrigger]` in this module, including the fully secured ones?
3. Explain `MapInboundClaims = false` to someone who has never seen a JWT
   claim type remapped -- what would they observe if that line were
   deleted, and why would it be confusing to debug without knowing this
   exercise?
4. What are the two separate things "Managed Identity" and "RBAC role
   assignment" each give you, and why does neither one alone let the
   Function read the Key Vault secret?
5. Why can't `appRegistrations.bicep`'s app registrations and role
   assignment be plain declarative Bicep resources, the way the Storage
   Account and Key Vault are?
6. Walk through the circular-dependency fix in `main.bicep` in your own
   words: what made it look circular, and what made it not actually be
   circular?
7. Name one thing that is a secret in this module's configuration, and one
   thing that looks like it should be secret but isn't. What's the
   distinguishing test you'd apply to a new configuration value to decide
   which category it falls into?
