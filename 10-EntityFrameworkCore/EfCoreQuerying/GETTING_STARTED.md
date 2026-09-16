# Getting Started with EfCoreQuerying

## Quick Start

### 1. Start PostgreSQL
This project shares one Postgres container with the rest of module 10, run
from the module's root:

```bash
cd 10-EntityFrameworkCore
docker compose up -d
```

This provisions (among others) an `efcore_querying` database on
`localhost:5432` (user `postgres`, password `postgres`). You only need to do
this once — it keeps running until you `docker compose down`.

### 2. Navigate to This Project
```bash
cd 10-EntityFrameworkCore/EfCoreQuerying
```

### 3. Verify Setup
```bash
dotnet build
```

### 4. What Is Already Here

```
EfCoreQuerying/
├── README.md                # The concepts behind each part
├── EXERCISE.md              # The work, in 10 parts (0 through 9)
├── GETTING_STARTED.md       # This file
│
├── EfCoreQuerying/          # <- YOUR WORKSPACE. Write your code here.
│   ├── EfCoreQuerying.csproj  #   ready to build
│   ├── Program.cs             #   replace as you work through Part 0
│   ├── appsettings.json       #   already points at efcore_querying
│   ├── Domain/                #   empty, waiting for your entities
│   ├── Persistence/           #   empty, waiting for your DbContext
│   ├── Seed/                  #   empty, waiting for your seeder
│   ├── Dtos/                  #   empty, waiting for your projection DTOs
│   ├── Queries/                #   empty, waiting for your compiled queries
│   └── Demos/                  #   empty, waiting for your per-part demos
│
├── solution/                # <- REFERENCE IMPLEMENTATION. Look after trying.
│   └── (same shape as above, fully implemented)
│
└── tests/                   # <- ~28 tests proving the behaviour, against real Postgres
```

The folders and `appsettings.json` already exist, so you can start typing
immediately rather than setting up scaffolding.

### 5. The Three Commands You Need

```bash
# Run your own work
dotnet run --project EfCoreQuerying

# Check your work against the tests (needs Docker -- see below)
dotnet test tests

# See the reference solution run, one part at a time
dotnet run --project solution -- 1      # Basic LINQ
dotnet run --project solution -- 2      # Projections vs. tracked entities
dotnet run --project solution -- 3      # Include/ThenInclude vs. projection
dotnet run --project solution -- 4      # AsSplitQuery
dotnet run --project solution -- 5      # GroupBy and aggregates
dotnet run --project solution -- 6      # Client vs. server evaluation
dotnet run --project solution -- 7      # Raw SQL
dotnet run --project solution -- 8      # PostgreSQL-only features
dotnet run --project solution -- 9      # Compiled queries
dotnet run --project solution -- all    # Everything in order
```

### How to Use the Reference Solution

`solution/` uses the same namespaces and type names as `EXERCISE.md`, so you
can compare your file against its counterpart directly.

Attempt each part yourself first. Open the reference when you are stuck or
when you have finished a part and want to compare approaches — reading it up
front is the fastest way to feel productive and learn nothing.

The tests point at `solution/` out of the box. To run them against **your**
code instead, edit the `ProjectReference` in `tests/EfCoreQuerying.Tests.csproj`:

```xml
<ProjectReference Include="../EfCoreQuerying/EfCoreQuerying.csproj" />
```

They will fail until you have written the types each test needs, which makes
them a usable checklist for how far you have got.

### About the Tests and Docker

The test suite uses [Testcontainers](https://testcontainers.com/), not the
`docker compose` container above — every `dotnet test` run starts its own
throwaway Postgres container, seeds it, runs, and tears it down. That means:
- `dotnet test tests` needs **Docker running**, but does **not** need
  `docker compose up -d` first (that's only for `dotnet run`).
- Test runs never affect the `efcore_querying` database you're using
  interactively via `dotnet run`.

### 6. Start Exercising
Open [EXERCISE.md](EXERCISE.md) and begin with **Part 0: The Domain Model,
DbContext, and Seed Data** — everything after it depends on having a schema
and data to query.

## Turning On Query Logging

Seeing the actual SQL EF Core generates is the fastest way to build
intuition for Parts 3, 4, 6, and 7 especially. Add this when building your
`DbContextOptions` in the workspace's `Program.cs`:

```csharp
var options = new DbContextOptionsBuilder<LibraryDbContext>()
    .UseNpgsql(connectionString)
    .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
    .EnableSensitiveDataLogging() // shows parameter VALUES, not just placeholders -- dev only
    .Options;
```

`EnableSensitiveDataLogging()` is deliberately opt-in and dev-only: it prints
real parameter values (book titles, prices, tags) into your console/log
output, which is exactly the kind of thing you don't want in a production
log stream.

## What You've Learned So Far

### From EfCoreModeling
- Fluent API entity configuration
- One-to-many, many-to-many, owned types, value converters
- The snake_case naming convention this project reuses

### From EfCoreMigrations
- Schema evolution, `HasData` seeding, `Database.Migrate()`

### Now: EfCoreQuerying
- **How** to get data back out efficiently: projections over tracking,
  `Include` vs. `Select`, split queries, server-side aggregates
- **Where** LINQ stops being the right tool, and how to drop into raw SQL
  without opening a SQL injection hole
- **What** PostgreSQL specifically gives you that a generic ORM tutorial
  (written against SQL Server or SQLite) never mentions

## Tips for Success

### 1. Run Every Part, Don't Just Read It
The whole point of Parts 4, 6, and 8 is behavior you can't predict from
reading the code alone — actually watching a cartesian explosion happen, or
an `InvalidOperationException` get thrown, or an `ILIKE` match a
differently-cased title, is what makes it stick.

### 2. Turn On Query Logging Early
See the "Turning On Query Logging" section above. Do this before Part 3, not
after — the SQL is the ground truth for every claim this exercise makes
about what's efficient and what isn't.

### 3. Don't Skip Part 6
It's short, but it's the part most likely to bite you in a real codebase
months from now, in a `Where` clause that used to work until someone added
one more condition that happened to call a C# method.

### 4. Connect to Real World
Think about your own projects:
- Where are you `Include`-ing data you only read one field from?
- Where might a multi-collection `Include` be silently moving far more data
  than the row count suggests?
- Where have you built SQL by concatenating strings, and could
  `FromSqlInterpolated` replace it safely?

## Common Mistakes

### "The query returns duplicate-looking rows"
You have more than one collection `Include` on the same root and haven't
used `AsSplitQuery()` — see Part 4. Check the raw SQL (query logging above)
to confirm before assuming it's a bug in your code.

### "InvalidOperationException: could not be translated"
You called a local C# method (or an unsupported overload) inside `Where`/
`OrderBy`/a projection — see Part 6. The fix is almost always either
rewriting the condition in translatable terms, or narrowing with SQL first
and filtering the (small) remainder in memory afterward.

### "My AsNoTracking() query still shows up in ChangeTracker.Entries()"
`AsNoTracking()` has to be called on the query itself, and doesn't retroactively
affect entities already tracked from a previous query on the same context.
Call `context.ChangeTracker.Clear()` if you need a clean slate before
comparing.

### "ILike/JsonContains/array Contains throws at runtime, not compile time"
These translate through the Npgsql provider specifically — make sure
`UseNpgsql(...)` is the provider configured on your `DbContextOptions`, and
that you're not accidentally running against SQLite or an in-memory
provider (which don't understand these calls at all).

## Troubleshooting

### "Connection refused" / "Is the server running on host 'localhost'"
Postgres isn't running. From `10-EntityFrameworkCore/`: `docker compose up -d`,
then wait a few seconds for the healthcheck to pass (`docker compose ps`
should show it as healthy).

### "database \"efcore_querying\" does not exist"
The container's first-boot init script creates it. If you started the
container before this project existed, or wiped the volume, run
`docker compose down -v && docker compose up -d` from `10-EntityFrameworkCore/`
to recreate everything from scratch (this deletes all data in every module
10 database, not just this one).

### `dotnet test` hangs or fails immediately
Testcontainers needs Docker running (not necessarily `docker compose up` —
see "About the Tests and Docker" above). If Docker Desktop isn't running,
start it first.

### "appsettings.json not found" at runtime
Check the `.csproj` has:
```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## Key Concepts to Internalize

### 1. Read-Only by Default
Most queries in a real application are read-only. Default to projection or
`AsNoTracking()`; reach for tracked `Include`d entities only when you're
about to mutate through the same context.

### 2. LINQ Is a Translation, Not Magic
Every LINQ query has to become SQL. When you can't picture the SQL a LINQ
expression would produce, that's a sign to either learn how EF Core would
translate it, or to write the SQL yourself.

### 3. Interpolation Is the Safety Mechanism, Not a Style Choice
`FromSqlInterpolated`/`ExecuteSqlInterpolatedAsync` with a C# `$"..."`
string parameterizes automatically. The visually similar `FromSqlRaw` with
concatenation does not. The method name is what matters.

### 4. Postgres-Specific Isn't a Compromise
`ILIKE`, array columns, and JSONB aren't workarounds — they're first-class
Postgres features EF Core exposes directly, often more expressive than what
a "portable" ORM-only approach could do.

## After Completing This Project

You'll understand:
- When to project vs. track, and what each costs
- How to read EF Core's generated SQL well enough to predict cartesian
  explosions before they happen
- How to recognize untranslatable LINQ before EF Core throws it back at you
- How to write raw SQL that's both expressive and injection-safe
- What PostgreSQL brings to querying that a lowest-common-denominator ORM
  tutorial never shows you

## Next Steps

1. Complete Parts 0 through 9
2. Compare against `solution/`
3. Run `dotnet test tests` and make sure everything passes
4. Move to **EfCoreTransactions**: explicit transactions, optimistic
   concurrency via Postgres's `xmin`, and isolation levels

---

**Ready to level up your queries?** Open [EXERCISE.md](EXERCISE.md) and start with Part 0!
