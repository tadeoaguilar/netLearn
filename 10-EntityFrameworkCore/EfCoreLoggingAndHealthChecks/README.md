# EfCoreLoggingAndHealthChecks - Observability and Operability for EF Core

## Overview
Every earlier EF Core project in this module treated `SaveChangesAsync()` as
a black box. This one opens it. You'll turn on EF Core's own SQL logging and
learn to narrow it, hook into the save and command pipelines with
interceptors to get automatic audit trails and slow-query detection, and
expose the database's real health through ASP.NET Core health checks -- the
same mechanism orchestrators like Kubernetes use to decide whether an
instance should receive traffic at all.

Unlike the other four projects in this module, this one is an **ASP.NET
Core minimal API**, not a console app -- health check endpoints need
something to host them.

## What You'll Learn

### Observability
- **`LogTo`**: EF Core's built-in SQL logging, and narrowing it to just the
  `Database.Command` category
- **`EnableSensitiveDataLogging()` / `EnableDetailedErrors()`**: what they
  actually reveal (real parameter values, not placeholders), and why that
  makes them dev-only
- **`UseLoggerFactory`**: routing EF Core's internal logs through the same
  `Microsoft.Extensions.Logging` pipeline as the rest of the app

### Automation via Interceptors
- **`SaveChangesInterceptor`**: stamping `CreatedAt`/`UpdatedAt` on every
  save, automatically, for every entity that opts in
- **`DbCommandInterceptor`**: timing every command and flagging the ones
  that cross a slow-query threshold

### Operability
- **Liveness vs. readiness**: why a liveness probe must never depend on the
  database, and what happens when that rule is broken
- **`AddDbContextCheck<T>`**: a real connectivity check, not a stub
- **A custom `IHealthCheck`**: catching schema drift with
  `Database.GetPendingMigrationsAsync()` before it becomes a confusing
  runtime error

## Why These Patterns Matter

### Real-World Scenarios

**Narrowed SQL logging**:
```csharp
// Everything, including connection lifecycle noise:
options.LogTo(Console.WriteLine, LogLevel.Information);

// Just the SQL:
options.LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name }, LogLevel.Information);
```

**Sensitive data logging, gated to dev**:
```csharp
if (builder.Environment.IsDevelopment())
{
    options.EnableSensitiveDataLogging();  // shows @p0='alice@example.com', not @p0='?'
    options.EnableDetailedErrors();
}
```

**Automatic audit stamping**:
```csharp
// Nobody writes product.CreatedAt = DateTime.UtcNow in an endpoint handler.
// AuditingSaveChangesInterceptor does it for every IAuditable entity, every save.
options.AddInterceptors(auditingInterceptor, slowQueryInterceptor);
```

**Liveness/readiness split**:
```csharp
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") }); // checks the DB
app.MapHealthChecks("/health/live",  new() { Predicate = c => c.Tags.Contains("live")  }); // never does
```

## Project Structure

```
EfCoreLoggingAndHealthChecks/
├── EfCoreLoggingAndHealthChecks/    # YOUR WORKSPACE
│   ├── EfCoreLoggingAndHealthChecks.csproj
│   ├── Program.cs
│   └── appsettings.json
├── solution/                        # REFERENCE IMPLEMENTATION
│   ├── EfCoreLoggingAndHealthChecks.Solution.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Models/
│   │   ├── IAuditable.cs
│   │   ├── Product.cs
│   │   ├── Order.cs
│   │   └── CreateOrderRequest.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   ├── SnakeCaseNaming.cs
│   │   └── DesignTimeDbContextFactory.cs
│   ├── Interceptors/
│   │   ├── AuditingSaveChangesInterceptor.cs
│   │   └── SlowQueryCommandInterceptor.cs
│   ├── HealthChecks/
│   │   └── PendingMigrationsHealthCheck.cs
│   └── Migrations/
│       └── ...InitialCreate...
├── tests/
│   ├── EfCoreLoggingAndHealthChecks.Tests.csproj
│   ├── Infrastructure/
│   │   ├── PostgresContainerFixture.cs
│   │   ├── TestApiFactory.cs
│   │   └── ListLoggerProvider.cs
│   └── ...test classes...
├── EXERCISE.md
├── GETTING_STARTED.md
└── README.md
```

## Quick Start

1. **Start PostgreSQL** (shared across this module, from
   `10-EntityFrameworkCore/`):
   ```bash
   docker compose up -d
   ```

2. **Navigate:**
   ```bash
   cd EfCoreLoggingAndHealthChecks
   ```

3. **Verify:**
   ```bash
   dotnet build EfCoreLoggingAndHealthChecks
   ```

4. **Start learning:**
   Open [EXERCISE.md](EXERCISE.md)

## The Learning Path

### Setup: Domain and DbContext (15 min)
`Product`/`Order`, both implementing `IAuditable`; the `AppDbContext`; the
snake_case naming convention carried over from module 09.

### Part 1: Simple SQL Logging (15 min)
`LogTo(Console.WriteLine, ...)`, then narrowed to just `Database.Command`
events.

**Use Case**: "What SQL did that endpoint actually run?"

### Part 2: Sensitive Data Logging and Detailed Errors (20 min)
What `EnableSensitiveDataLogging()` reveals, and why it's a compliance risk
outside development.

**Use Case**: Debugging a failing query locally without leaking PII in a
production log aggregator.

### Part 3: Auditing SaveChangesInterceptor (30 min)
Automatic `CreatedAt`/`UpdatedAt` stamping for every entity that implements
`IAuditable`.

**Use Case**: Audit trails that can't be forgotten because they don't live
in application code.

### Part 4: Slow-Query DbCommandInterceptor (30 min)
Timing every executed command using EF Core's own `CommandExecutedEventData.Duration`,
warning when it crosses a threshold.

**Use Case**: A lightweight, always-on tripwire instead of reaching for a
profiler only after users complain.

### Part 5: EF Core Logs Through `ILoggerFactory` (20 min)
Contrasting `LogTo`'s separate sink with routing EF Core's logs through the
app's own logging pipeline -- same categories, same levels, same structured
output.

**Use Case**: One logging pipeline, one place to configure where logs go.

### Part 6: Liveness and Readiness Health Checks (25 min)
`AddDbContextCheck<AppDbContext>` tagged `"ready"`, a no-dependency check
tagged `"live"`, and tag-filtered `MapHealthChecks` endpoints.

**Use Case**: Telling Kubernetes "don't route to me" apart from "kill and
restart me" -- getting this wrong turns a database blip into a full outage.

### Part 7: Custom Pending-Migrations Health Check (30 min)
A real migration, generated with `dotnet ef migrations add`, checked against
with `Database.GetPendingMigrationsAsync()`.

**Use Case**: Catching a deploy that shipped code ahead of its database
schema, before it fails confusingly mid-request.

**Total Time**: ~3 hours

## Pattern Deep Dive

### 1. Narrowed Logging vs. Full Pipeline Integration

**Problem**: `LogTo(Console.WriteLine, ...)` is simple but isolated --
separate from the rest of the app's logs, unfilterable by
`appsettings.json`, unstructured.

**Solution**: `UseLoggerFactory` hands EF Core the app's real
`ILoggerFactory`.

```csharp
options.UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>());
```

**When to Use Full Integration**: Any real app. `LogTo` is for a quick local
debugging session; `UseLoggerFactory` is what ships.

---

### 2. Interceptors as Cross-Cutting Concerns

**Problem**: Audit timestamps and slow-query detection, done per-endpoint,
get forgotten.

**Solution**: Hook the save/command pipeline once, centrally.

```csharp
public class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        // stamps every IAuditable entity being tracked, every save
    }
}
```

**When to Use**: Any behavior that should apply to *every* save or *every*
command, without relying on every call site remembering to opt in.

---

### 3. Tagged Health Checks

**Problem**: A single `/health` endpoint that checks everything conflates
"broken and unroutable" with "process needs to be killed."

**Solution**: Tag checks, filter endpoints by tag.

```csharp
.AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
.AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
```

**When to Use**: Always, once an app has any real dependency (a database, a
downstream API, a cache). A liveness probe with zero dependencies is what
keeps a dependency outage from becoming a restart storm.

---

## Key Patterns Comparison

| Pattern | Use When | Example |
|---|---|---|
| `LogTo` | Quick local debugging | See raw SQL in the console immediately |
| `UseLoggerFactory` | Any real app | EF logs alongside app logs, filterable, structured |
| `SaveChangesInterceptor` | Behavior on every save | Audit timestamps |
| `DbCommandInterceptor` | Behavior on every command | Slow-query warnings |
| `AddDbContextCheck<T>` | Readiness | "Can I actually reach the database?" |
| No-dependency check | Liveness | "Is the process itself alive?" |
| Custom `IHealthCheck` | Readiness, app-specific | "Does the schema match what I expect?" |

## Common Mistakes to Avoid
- Enabling `EnableSensitiveDataLogging()` without gating it behind
  `IsDevelopment()`
- Constructing an interceptor with `new` instead of registering it as a
  service (breaks the moment it needs a dependency, like `ILogger`)
- Making a liveness check touch the database, cache, or any other external
  dependency
- Calling `Database.Migrate()` automatically at startup, so a briefly
  unreachable database takes down the whole process instead of just failing
  a readiness check
- Forgetting `public partial class Program;`, which breaks
  `WebApplicationFactory<Program>` in the tests project

## Troubleshooting
See [GETTING_STARTED.md](GETTING_STARTED.md#troubleshooting) for connection
string, migration, and Testcontainers issues.

## Next Steps
This is the last project in the last module of this repository. From here:

1. **Review**: Compare your workspace implementation against `solution/`
   part by part.
2. **Revisit [09-EnterpriseCRUD](../../09-EnterpriseCRUD/)**: its
   `TaskManagement.Infrastructure` layer already uses the snake_case
   convention, audit fields, and health checks this project builds from
   first principles.
3. **Apply it**: the interceptor and health-check patterns here transfer
   directly to any EF Core project with real production traffic.

---

**Ready to begin?** Open [EXERCISE.md](EXERCISE.md) and start with **Setup**.
