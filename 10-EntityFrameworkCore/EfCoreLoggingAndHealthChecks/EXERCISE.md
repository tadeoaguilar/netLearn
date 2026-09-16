# Exercise: EF Core Logging, Interceptors, and Health Checks

## Overview
Every earlier module used EF Core as a black box: call `SaveChangesAsync()`,
trust that the right SQL happened. This exercise opens the box. You'll watch
the actual SQL EF Core sends, hook into the save pipeline to stamp audit
fields and catch slow queries automatically, and expose the database's real
health to the outside world through ASP.NET Core health checks -- the same
mechanism Kubernetes and other orchestrators use to decide whether to route
traffic to an instance.

The domain is deliberately tiny (`Product`, `Order`) so nothing here is about
modeling. It exists purely to generate real EF Core activity for the
logging and interceptor parts to observe.

## Learning Goals
By completing this exercise, you will:
- Turn on EF Core's built-in SQL logging, then narrow it to just the events
  you care about
- Understand exactly what `EnableSensitiveDataLogging()` reveals, and why
  that makes it a dev-only setting
- Write a `SaveChangesInterceptor` that stamps audit timestamps automatically
- Write a `DbCommandInterceptor` that flags slow queries as they happen
- Wire EF Core's internal logs through the same `ILoggerFactory` pipeline as
  the rest of the app
- Add ASP.NET Core health checks that tell liveness and readiness apart, and
  understand why that distinction matters operationally
- Write a custom health check that catches schema drift using
  `GetPendingMigrationsAsync()`

---

## Setup

### The connection string
This module's `docker-compose.yml` (one level up, in `10-EntityFrameworkCore/`)
provisions an `efcore_healthchecks` database for you. Start it once, from
that folder:

```bash
docker compose up -d
```

`appsettings.json` already points at it:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=efcore_healthchecks;Username=postgres;Password=postgres"
  }
}
```

### The domain
**Your Task:**
Create `Models/IAuditable.cs`, `Models/Product.cs`, `Models/Order.cs`, and
`Models/CreateOrderRequest.cs`:

```csharp
// Models/IAuditable.cs
namespace EfCoreLoggingAndHealthChecks.Models;

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
```

```csharp
// Models/Product.cs
namespace EfCoreLoggingAndHealthChecks.Models;

public class Product : IAuditable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

```csharp
// Models/Order.cs
namespace EfCoreLoggingAndHealthChecks.Models;

public class Order : IAuditable
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

```csharp
// Models/CreateOrderRequest.cs
namespace EfCoreLoggingAndHealthChecks.Models;

public record CreateOrderRequest(int ProductId, int Quantity);
```

Both entities implement `IAuditable` on purpose -- Part 3's interceptor stamps
`CreatedAt`/`UpdatedAt` on *every* entity that implements it, not just one
hand-picked type. That is the whole point of doing this as a cross-cutting
concern instead of setting the timestamps in each endpoint handler.

### The naming convention
**Your Task:**
Copy `Data/SnakeCaseNaming.cs` from module 09
(`09-EnterpriseCRUD/src/TaskManagement.Infrastructure/Persistence/SnakeCaseNaming.cs`)
into your own `Data/SnakeCaseNaming.cs`, changing only the namespace to
`EfCoreLoggingAndHealthChecks.Data`. It renames every column, key, foreign
key and index to snake_case -- PostgreSQL's convention -- as a **convention**
applied once in `OnModelCreating`, rather than something anyone has to
remember to do per property.

### The DbContext
**Your Task:**
Create `Data/AppDbContext.cs`:

```csharp
using EfCoreLoggingAndHealthChecks.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLoggingAndHealthChecks.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasOne(o => o.Product)
                .WithMany()
                .HasForeignKey(o => o.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Applied last so it renames whatever the configuration above produced.
        modelBuilder.UseSnakeCaseNames();

        base.OnModelCreating(modelBuilder);
    }
}
```

**Questions to think about:**
1. Why does `UseSnakeCaseNames()` need to run *after* the entity
   configurations, not before?
2. `Order.Product` has no `[Required]` attribute anywhere -- what makes it
   required in the database anyway?

---

## Part 1: Simple SQL Logging

### Why?
The single most common EF Core question is "what SQL did that actually run?"
`LogTo` answers it directly, no profiler required.

**Your Task:**
In `Program.cs`, register `AppDbContext` and turn on `LogTo`:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default")!;
    options.UseNpgsql(connectionString);

    options.LogTo(Console.WriteLine, LogLevel.Information);
});
```

**Run it**, hit `POST /products` (once you've added the endpoint further
down), and watch the console. You'll see connection open/close events, the
generated SQL, parameter placeholders, and EF's own internal bookkeeping --
all mixed together.

### Step 1.2: Narrow it to just the SQL
That's a lot of noise for "what SQL ran?" `LogTo`'s second overload accepts
specific categories. `DbLoggerCategory.Database.Command.Name` is exactly the
category EF Core uses for the commands it sends to the database:

```csharp
options.LogTo(
    Console.WriteLine,
    new[] { DbLoggerCategory.Database.Command.Name },
    LogLevel.Information);
```

**Run it again.** Now the console only shows command text and parameters --
connection lifecycle and change-tracking noise are gone.

**Questions to think about:**
1. What other categories does `DbLoggerCategory` expose? (Hint: look at
   `DbLoggerCategory.Database.Connection`, `.Transaction`, and
   `DbLoggerCategory.ChangeTracking`.)
2. `LogTo(Console.WriteLine, ...)` writes straight to the console, bypassing
   `Microsoft.Extensions.Logging` entirely. What does that cost you? (Keep
   this question in mind -- Part 5 answers it.)

---

## Part 2: Sensitive Data Logging and Detailed Errors

### Why -- and why this is dangerous
By default, EF Core's logs show parameter *placeholders*, not values:
`INSERT INTO products (name, price) VALUES (@p0, @p1)`. That's deliberate.

**Your Task:**
Add these two lines, gated behind `IsDevelopment()`:

```csharp
if (builder.Environment.IsDevelopment())
{
    options.EnableSensitiveDataLogging();
    options.EnableDetailedErrors();
}
```

**Run it** and create a product again. Compare the log line before and after
adding `EnableSensitiveDataLogging()`. Before, you see `@p0='?'`. After, you
see the actual value: `@p0='Widget'`.

### Why this matters in production
`EnableSensitiveDataLogging()` puts real data -- names, prices, and in a
less toy domain, emails, SSNs, tokens -- into every log line touching the
database. Most production systems ship logs to a centralized aggregator
(Splunk, Datadog, CloudWatch, ...) where they're searchable by anyone with
log access and often retained for months. That turns a debugging convenience
into a real compliance problem (GDPR, PCI-DSS, HIPAA all care about exactly
this). `EnableDetailedErrors()` is milder -- it gives fuller exception
messages instead of EF Core's generic "see the log for the failing command"
-- but it too can leak schema and query shape information you don't want an
attacker to see in a production stack trace.

**Both belong behind an environment check, never enabled unconditionally.**

**Questions to think about:**
1. If a teammate asks "just enable sensitive data logging in production
   temporarily, we need to debug this", what would you tell them to do
   instead?
2. What's the difference in scope between what `EnableSensitiveDataLogging()`
   reveals and what `EnableDetailedErrors()` reveals?

---

## Part 3: An Auditing SaveChangesInterceptor

### Why?
Every entity in this project implements `IAuditable`. Setting
`CreatedAt`/`UpdatedAt` by hand in every endpoint is exactly the kind of
thing that gets forgotten the third time someone adds a new entity. An
interceptor runs for **every** `SaveChanges`/`SaveChangesAsync` call, from
anywhere in the app, so it can't be skipped.

**Your Task:**
Create `Interceptors/AuditingSaveChangesInterceptor.cs`:

```csharp
using EfCoreLoggingAndHealthChecks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLoggingAndHealthChecks.Interceptors;

public class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        StampAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StampAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void StampAuditFields(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    break;
            }
        }
    }
}
```

Register it and hand it to the `DbContext`:

```csharp
builder.Services.AddSingleton<AuditingSaveChangesInterceptor>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    // ...UseNpgsql, LogTo, etc. from Parts 1-2...

    options.AddInterceptors(
        serviceProvider.GetRequiredService<AuditingSaveChangesInterceptor>());
});
```

**Run it.** Create a product, then update it (you'll need a `PUT` or a second
save -- even just changing `product.Price` and calling `SaveChangesAsync()`
again works). Query the row and confirm `CreatedAt` never changes but
`UpdatedAt` does.

**Questions to think about:**
1. Why register the interceptor as a **singleton** service rather than
   constructing it with `new AuditingSaveChangesInterceptor()` inline?
2. The `Modified` branch explicitly sets
   `entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false`. What
   would happen without that line if a caller accidentally changed
   `CreatedAt` on a tracked entity before saving?

---

## Part 4: A Slow-Query DbCommandInterceptor

### Why?
Profilers are for investigating a known problem. This is for finding out
you *have* one -- a lightweight, always-on tripwire that logs a warning the
moment any command crosses a threshold, so a slow endpoint shows up in
ordinary logs instead of requiring someone to go looking for it.

**Your Task:**
Create `Interceptors/SlowQueryCommandInterceptor.cs`:

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLoggingAndHealthChecks.Interceptors;

public class SlowQueryCommandInterceptor : DbCommandInterceptor
{
    public static readonly TimeSpan SlowQueryThreshold = TimeSpan.FromMilliseconds(500);

    private readonly ILogger<SlowQueryCommandInterceptor> _logger;

    public SlowQueryCommandInterceptor(ILogger<SlowQueryCommandInterceptor> logger)
    {
        _logger = logger;
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        LogIfSlow(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void LogIfSlow(DbCommand command, CommandExecutedEventData eventData)
    {
        if (eventData.Duration < SlowQueryThreshold) return;

        _logger.LogWarning(
            "Slow query detected ({DurationMs}ms, threshold {ThresholdMs}ms): {CommandText}",
            eventData.Duration.TotalMilliseconds,
            SlowQueryThreshold.TotalMilliseconds,
            command.CommandText);
    }
}
```

Notice there's no manual `Stopwatch` -- `CommandExecutedEventData.Duration`
is EF Core's own measurement of how long the command took, handed to you for
free.

Register it the same way as the auditing interceptor, and add it to
`AddInterceptors(...)` alongside it.

**Run it.** You don't have a naturally slow query in this tiny domain, so
force one to prove the interceptor works:

```csharp
await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_sleep(0.6)");
```

You should see a warning-level log line. Run an ordinary `GET /products`
and confirm you *don't* see one.

**Questions to think about:**
1. This interceptor overrides `ReaderExecuted`/`ReaderExecutedAsync`. What
   other `DbCommandInterceptor` methods exist, and which command types would
   they need overriding for (a plain `SaveChangesAsync()` insert, for
   instance)?
2. 500ms is a reasonable default for this exercise. What would you look at
   to pick a real threshold for a production endpoint?

---

## Part 5: EF Core Logs Through the App's Own Logging Pipeline

### Why?
Part 1's `LogTo(Console.WriteLine, ...)` is simple but limited: it's a
separate sink from the rest of the app's logs, with no categories your
`appsettings.json` `Logging` section can filter, no structured fields, and no
way to route it anywhere else (a file, an aggregator, whatever the rest of
the app uses). Real apps want EF Core's logs flowing through the *same*
`ILogger`/`ILoggerFactory` pipeline as everything else.

**Your Task:**
Replace `LogTo` with `UseLoggerFactory`:

```csharp
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(connectionString);

    options.UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>());

    // ...interceptors, sensitive data logging...
});
```

**Run it.** The SQL now appears in the console through the standard ASP.NET
Core logging format (with a timestamp, category, and log level prefix) --
because it's going through the exact same pipeline as
`app.Logger.LogInformation(...)` would. Try changing
`appsettings.json`'s `Logging:LogLevel:Microsoft.EntityFrameworkCore` to
`"Warning"` and confirm the SQL noise disappears while your own app logs
don't.

**Questions to think about:**
1. Can `LogTo` and `UseLoggerFactory` be used at the same time? What would
   happen if you did?
2. Why does `UseLoggerFactory` need to pull `ILoggerFactory` from the
   `IServiceProvider` passed into the `AddDbContext` callback, rather than
   just calling `LoggerFactory.Create(...)` directly inside it?

---

## Part 6: Liveness and Readiness Health Checks

### Why the split?
A **readiness** probe asks "should traffic be routed here right now?" A
**liveness** probe asks "is this process itself wedged and needs to be
killed and restarted?" Conflating them is a classic outage amplifier: if a
liveness probe also checks the database, a database blip makes an
orchestrator kill and restart *every* healthy instance -- which fixes
nothing (the database is still down) and drops every in-flight request in
the process.

**Your Task:**
Register the checks and map both endpoints:

```csharp
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
    .AddCheck("self", () => HealthCheckResult.Healthy("process is up"), tags: ["live"]);
```

```csharp
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
```

`AddDbContextCheck<AppDbContext>` calls `Database.CanConnectAsync()` under
the hood -- a real connectivity check, not a stub. The `"self"` check never
touches the database at all.

**Run it.** Hit both endpoints while the database is running -- both should
report Healthy. Stop the container (`docker compose stop` in
`10-EntityFrameworkCore/`) and hit both again: `/health/ready` should now
fail while `/health/live` stays Healthy. Restart the container afterwards.

**Questions to think about:**
1. Why is `Predicate` the mechanism for telling the two endpoints apart,
   rather than registering two completely separate `AddHealthChecks()`
   calls?
2. What should happen to a request already in flight when `/health/ready`
   starts failing? What should happen to a *new* request?

---

## Part 7: A Custom Pending-Migrations Health Check

### Why?
A deploy that ships new code without first applying the matching database
migration doesn't fail at startup -- it fails confusingly, later, at the
first query that touches a column or table that doesn't exist yet, deep
inside some unrelated request. `Database.GetPendingMigrationsAsync()` can
catch that *before* it becomes a runtime error, by comparing the migrations
built into the running assembly against what's actually been applied to the
connected database.

### Step 7.1: Generate a real migration
This check is only meaningful against a real migration, so create one:

```bash
export PATH="$PATH:$HOME/.dotnet/tools"   # if `dotnet ef` isn't found
dotnet ef migrations add InitialCreate
```

If `dotnet ef` complains it can't create an `AppDbContext` (it tries to build
your whole `Program.cs` to find one, and a half-finished `Program.cs` can
confuse it), add a small `IDesignTimeDbContextFactory<AppDbContext>` --
`solution/Data/DesignTimeDbContextFactory.cs` is a working example to copy
from. It sidesteps the app's hosting pipeline entirely for tooling purposes,
and only needs a syntactically valid connection string, not a reachable one.

Inspect the generated file under `Migrations/`. Notice the table and column
names are already snake_case -- `UseSnakeCaseNames()` from the setup section
applies to migrations too, since they're generated from the same model.

Apply it to your local database once, the same way a deployment would:

```bash
dotnet ef database update
```

### Step 7.2: The check
**Your Task:**
Create `HealthChecks/PendingMigrationsHealthCheck.cs`:

```csharp
using EfCoreLoggingAndHealthChecks.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EfCoreLoggingAndHealthChecks.HealthChecks;

public class PendingMigrationsHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public PendingMigrationsHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = (await _dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken))
                .ToList();

            return pending.Count == 0
                ? HealthCheckResult.Healthy("no pending migrations")
                : HealthCheckResult.Unhealthy(
                    $"{pending.Count} pending migration(s): {string.Join(", ", pending)}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("could not determine pending migrations", ex);
        }
    }
}
```

Add it to the readiness checks:

```csharp
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
    .AddCheck<PendingMigrationsHealthCheck>("pending-migrations", tags: ["ready"])
    .AddCheck("self", () => HealthCheckResult.Healthy("process is up"), tags: ["live"]);
```

**Run it.** With the migration applied, `/health/ready` should be Healthy.
Now simulate drift: `dotnet ef database update 0` rolls every migration
back, un-applying `InitialCreate` without dropping your ability to re-apply
it. Hit `/health/ready` again -- it should now report Unhealthy, naming the
pending migration. Run `dotnet ef database update` once more to restore it.

### A deliberate design choice: no auto-migration at startup
You might expect `Program.cs` to call `await db.Database.MigrateAsync()`
once at startup so this is never a manual step. This project deliberately
does **not** do that: a process that tries to migrate the database as part
of booting will crash entirely the moment the database is briefly
unreachable -- before health checks even get a chance to report *why*, and
before an orchestrator can tell "still starting up" apart from "broken".
Applying migrations is a deploy step (`dotnet ef database update`, or a CI/CD
pipeline stage), kept separate from the process starting and reporting its
own health honestly.

**Questions to think about:**
1. Why does the check return `Unhealthy` from the `catch` block instead of
   letting the exception propagate?
2. This check and `AddDbContextCheck<AppDbContext>` can both fail for the
   same underlying reason (the database is unreachable). Is that
   duplication a problem? What does each one tell you that the other
   doesn't?

---

## Minimal API Endpoints
Wire up trivial endpoints so there's something to generate activity for the
parts above to observe:

```csharp
app.MapGet("/products", async (AppDbContext db) =>
    Results.Ok(await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync()));

app.MapPost("/products", async (Product product, AppDbContext db) =>
{
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{product.Id}", product);
});

app.MapGet("/orders", async (AppDbContext db) =>
    Results.Ok(await db.Orders.AsNoTracking().OrderBy(o => o.Id).ToListAsync()));

app.MapPost("/orders", async (CreateOrderRequest request, AppDbContext db) =>
{
    if (request.Quantity <= 0)
        return Results.BadRequest(new { error = "Quantity must be positive." });

    if (!await db.Products.AnyAsync(p => p.Id == request.ProductId))
        return Results.NotFound(new { error = $"Product {request.ProductId} does not exist." });

    var order = new Order { ProductId = request.ProductId, Quantity = request.Quantity };
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/orders/{order.Id}", order);
});
```

Also add `public partial class Program;` at the very bottom of `Program.cs`
-- integration tests need it to reference the entry point via
`WebApplicationFactory<Program>`.

---

## Checking Your Work
A complete reference implementation lives in `solution/`, and `tests/`
proves the behaviour end-to-end against a real, disposable Postgres
(Testcontainers) plus the real minimal API
(`Microsoft.AspNetCore.Mvc.Testing`):

```bash
dotnet run --project solution      # watch it run
dotnet test tests                  # prove it behaves (needs Docker)
```

Pay particular attention to `tests/HealthEndpointsTests.cs` --
`HealthEndpointsRedPathTests` points the app at an unreachable database on
purpose and asserts `/health/ready` fails while `/health/live` doesn't. That
test is the liveness/readiness distinction made concrete.

---

## Reflection Questions

1. **What's the practical difference between `LogTo` and `UseLoggerFactory`?**
   When would you reach for each?

2. **Why is `EnableSensitiveDataLogging()` a security concern specifically in
   production, when it's perfectly safe (even useful) in development?**

3. **Why do interceptors belong in `AddInterceptors(...)` at the `DbContext`
   level instead of being called manually wherever `SaveChanges` happens?**

4. **Why must a liveness probe never depend on the database (or any other
   external dependency)?**

5. **Walk through what happens, end to end, when a deploy ships code
   expecting a new column that the database doesn't have yet -- with and
   without the pending-migrations health check in place.**

---

## Summary

You've learned:
- Turning on and narrowing EF Core's SQL logging with `LogTo`
- What `EnableSensitiveDataLogging()`/`EnableDetailedErrors()` actually
  reveal, and why they're dev-only
- Writing a `SaveChangesInterceptor` for automatic audit stamping
- Writing a `DbCommandInterceptor` for automatic slow-query detection
- Wiring EF Core's logs through the app's own `ILoggerFactory`
- Splitting liveness from readiness with tagged health checks
- Catching schema drift with a custom `GetPendingMigrationsAsync()` check

## Next Steps
This is the last project in the last module of this repository. Revisit
[09-EnterpriseCRUD](../../09-EnterpriseCRUD/) and notice how much of its
`TaskManagement.Infrastructure` layer -- the snake_case convention, the
audit fields, the health checks -- you can now explain from first
principles instead of taking on faith.
