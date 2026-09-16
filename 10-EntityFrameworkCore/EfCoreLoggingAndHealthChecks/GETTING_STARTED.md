# Getting Started with EfCoreLoggingAndHealthChecks

## Quick Start

### 1. Start PostgreSQL
This project shares a Postgres container with the rest of module 10. From
the module root:

```bash
cd 10-EntityFrameworkCore
docker compose up -d
```

This provisions (among others) an `efcore_healthchecks` database on
`localhost:5432` (user `postgres`, password `postgres`).

### 2. Navigate to the Project
```bash
cd 10-EntityFrameworkCore/EfCoreLoggingAndHealthChecks
```

### 3. Verify Setup
```bash
dotnet build EfCoreLoggingAndHealthChecks
```

### 4. What Is Already Here

```
EfCoreLoggingAndHealthChecks/
├── README.md                 # The concepts behind each part
├── EXERCISE.md               # The work, in 7 parts + setup
├── GETTING_STARTED.md        # This file
│
├── EfCoreLoggingAndHealthChecks/   # <- YOUR WORKSPACE. Write your code here.
│   ├── EfCoreLoggingAndHealthChecks.csproj
│   ├── Program.cs             #   minimal API entry point, ready to extend
│   └── appsettings.json       #   connection string already filled in
│
├── solution/                  # <- REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Models/                #   Product, Order, IAuditable
│   ├── Data/                  #   AppDbContext, SnakeCaseNaming, design-time factory
│   ├── Interceptors/          #   the two interceptors from Parts 3-4
│   ├── HealthChecks/          #   the custom check from Part 7
│   ├── Migrations/            #   a real, generated InitialCreate migration
│   └── Program.cs             #   everything wired together
│
└── tests/                     # <- tests proving the behaviour, Testcontainers-backed
```

The folders already exist, so you can start creating files immediately
rather than setting up scaffolding.

### 5. The Commands You Need

```bash
# Run your own work
dotnet run --project EfCoreLoggingAndHealthChecks

# Run the reference solution
dotnet run --project solution

# Check your work against the tests (needs Docker; each test class starts
# its own throwaway Postgres via Testcontainers)
dotnet test tests

# Apply the reference solution's migration to your local `efcore_healthchecks`
# database (run from inside solution/)
cd solution && dotnet ef database update
```

### How to Use the Reference Solution
`solution/` uses the same namespace (`EfCoreLoggingAndHealthChecks`) and the
same type names as `EXERCISE.md`, so you can compare your file against its
counterpart directly, file for file.

Attempt each part yourself first. Open the reference when you're stuck, or
once you've finished a part and want to compare approaches -- reading it
up front is the fastest way to feel productive and learn nothing.

The tests point at `solution/` out of the box. To run them against **your**
code instead, edit the `ProjectReference` in
`tests/EfCoreLoggingAndHealthChecks.Tests.csproj`:

```xml
<ProjectReference Include="../EfCoreLoggingAndHealthChecks/EfCoreLoggingAndHealthChecks.csproj" />
```

They will fail until you've written the types each test needs, which makes
them a usable checklist for how far you've got.

### 6. Start Exercising
Open [EXERCISE.md](EXERCISE.md) and begin with **Setup**, then **Part 1**.

## What You've Learned So Far (Module 10)

### From EfCoreModeling / EfCoreMigrations
- Fluent API configuration, relationships, and inheritance
- Creating and applying migrations, hand-editing generated migration code

### From EfCoreQuerying / EfCoreTransactions
- Writing LINQ that translates well, PostgreSQL-specific querying
- Explicit transactions, optimistic concurrency, isolation levels

### Now: EfCoreLoggingAndHealthChecks
- **Observability**: seeing exactly what EF Core does, not just trusting it
- **Automation**: cross-cutting concerns (audit stamps, slow-query
  detection) that no one has to remember to apply per-endpoint
- **Operability**: telling an orchestrator, honestly, whether this instance
  should receive traffic

## The Part Overview

| Part | Learns | Key API |
|---|---|---|
| 1 | Basic SQL logging | `optionsBuilder.LogTo(...)` |
| 2 | Sensitive data / detailed errors, dev-only | `EnableSensitiveDataLogging()` |
| 3 | Automatic audit stamping | `SaveChangesInterceptor` |
| 4 | Automatic slow-query detection | `DbCommandInterceptor` |
| 5 | EF logs through the app's own pipeline | `UseLoggerFactory(...)` |
| 6 | Liveness vs. readiness | `MapHealthChecks` + tag `Predicate` |
| 7 | Catching schema drift | `Database.GetPendingMigrationsAsync()` |

## Tips for Success

### 1. Run after each part
Hit the relevant endpoint (`POST /products`, `POST /orders`, `/health/ready`,
`/health/live`) after every part and actually read the console output. Most
of what this exercise teaches only shows up there.

### 2. Break things on purpose
Stop the Postgres container and watch `/health/ready` fail while
`/health/live` doesn't. Roll a migration back with `dotnet ef database
update 0` and watch the pending-migrations check catch it. Seeing the
failure mode is most of the point.

### 3. Compare log output before and after Part 5
Part 1's `LogTo(Console.WriteLine, ...)` and Part 5's `UseLoggerFactory(...)`
produce visibly different output for the same query. Notice the difference
before moving on.

### 4. Connect to real deployments
Every part here maps to something a real production system does: structured
logging shipped to an aggregator, audit trails, slow-query alerting,
Kubernetes liveness/readiness probes, deploy-time schema checks. None of it
is exercise-only ceremony.

## Common Patterns You'll Write

### Registering interceptors that need dependencies
```csharp
builder.Services.AddSingleton<SlowQueryCommandInterceptor>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(serviceProvider.GetRequiredService<SlowQueryCommandInterceptor>());
});
```

### Splitting health checks by tag
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/health/live", new() { Predicate = c => c.Tags.Contains("live") });
```

## Troubleshooting

### "Cannot connect to database" / health checks always Unhealthy
- Is Postgres running? `docker compose up -d` from `10-EntityFrameworkCore/`
- Check the connection string in `appsettings.json` matches
  `docker-compose.yml` (`efcore_healthchecks` / `postgres` / `postgres`)
- Has the migration actually been applied? `cd solution && dotnet ef
  database update`

### "relation \"products\" does not exist"
- The migration hasn't been applied to your local database yet. Run
  `dotnet ef database update` from `solution/` (or your workspace project,
  once it has its own migration).

### `dotnet ef migrations add` can't find `AppDbContext`
- Add an `IDesignTimeDbContextFactory<AppDbContext>` -- see
  `solution/Data/DesignTimeDbContextFactory.cs` for a working example. It
  only needs a syntactically valid connection string, not a reachable one.

### `WebApplicationFactory<Program>` can't find `Program`
- `Program.cs` needs `public partial class Program;` at the very bottom.
  Top-level statements compile into an implicit `Program` class in the
  global namespace; this line just makes it `public` so the tests project
  can reference it.

### Tests hang or fail with a Docker error
- The tests need Docker running (Testcontainers starts real, throwaway
  Postgres containers) -- this is separate from `docker compose up -d`,
  which is only for running the app yourself. `dotnet test` does not need
  `docker compose up` first.

### `EnableSensitiveDataLogging()` doesn't seem to change anything
- Check you're actually running in the `Development` environment --
  `DOTNET_ENVIRONMENT=Development dotnet run` if in doubt. The whole point
  of gating it behind `IsDevelopment()` is that it's silently a no-op
  everywhere else.

## After Completing This Project
You'll be able to:
- Read and narrow EF Core's own SQL logs
- Explain, concretely, why sensitive data logging is a production risk
- Write interceptors for cross-cutting save/command concerns
- Route EF Core's internal logs through the app's logging pipeline
- Design liveness/readiness health checks that fail safely
- Catch schema drift before it becomes a runtime error

This is also the last project in the last module of this repository. From
here, revisit [09-EnterpriseCRUD](../../09-EnterpriseCRUD/) -- its
`TaskManagement.Infrastructure` layer uses every idea from this module,
including this project's audit-stamping and health-check patterns.

---

**Ready to start?** Open [EXERCISE.md](EXERCISE.md) and begin with **Setup**.
