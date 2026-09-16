# Getting Started with EfCoreMigrations

## Quick Start

### 1. Start PostgreSQL
This module shares one Postgres container across all five projects. From the
module root:
```bash
cd 10-EntityFrameworkCore
docker compose up -d
```
This provisions an `efcore_migrations` database on `localhost:5432` (user
`postgres`, password `postgres`) alongside the databases the other projects
use. You only need to do this once per session; leave it running.

### 2. Navigate to This Project
```bash
cd EfCoreMigrations
```

### 3. Verify Setup
```bash
dotnet build ../../netLearn.sln
```

### 4. What Is Already Here

```
EfCoreMigrations/
├── README.md                 # The concepts behind each part
├── EXERCISE.md               # The work, in 6 parts
├── GETTING_STARTED.md        # This file
│
├── EfCoreMigrations/          # <- YOUR WORKSPACE. Write your code here.
│   ├── EfCoreMigrations.csproj  #   ready to build, EF Core packages included
│   ├── Program.cs               #   replace as you work through each part
│   └── appsettings.json         #   points at the efcore_migrations database
│                                 #   (no Migrations/ folder -- you create it
│                                 #    yourself with `dotnet ef migrations add`)
│
├── solution/                  # <- REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Models/                #   Author, Book, Genre
│   ├── Data/                  #   DbContext, design-time factory, snake_case
│   │   └── Configurations/    #   convention, Fluent API configs, runtime seed
│   └── Migrations/            #   five real, generated migrations
│
└── tests/                     # <- Testcontainers-backed schema tests
```

The workspace folder is intentionally close to empty -- this project is
about the `dotnet ef` workflow itself, and typing the commands yourself is
most of the point.

### 5. The Commands You'll Use Constantly

```bash
# From EfCoreMigrations/EfCoreMigrations/ (your workspace):
dotnet ef migrations add <Name> -o Migrations   # scaffold a migration
dotnet ef database update                       # apply all pending migrations
dotnet ef database update <MigrationName>       # roll forward/back to a specific point
dotnet ef migrations remove                      # delete the last, unapplied migration
dotnet ef migrations list                        # see history + pending status

# Run your own work
dotnet run

# Check your work against the tests (needs Docker)
dotnet test ../tests

# See the reference solution run
dotnet run --project ../solution
```

If `dotnet ef` isn't recognized, install it once (per machine, not per
project):
```bash
dotnet tool install --global dotnet-ef
```

### How to Use the Reference Solution

`solution/` uses the same namespaces and type names as `EXERCISE.md`, so you
can compare your file against its counterpart directly. Its `Migrations/`
folder is the real output of running the same `dotnet ef migrations add`
commands EXERCISE.md walks you through -- not hand-written to look right,
actually generated and applied during this project's construction.

Attempt each part yourself first. Open the reference when you're stuck, or
once you've finished a part and want to compare your generated migration
against the real one.

The tests point at `solution/` out of the box. To run them against **your**
code instead, edit the `ProjectReference` in
`tests/EfCoreMigrations.Tests.csproj`:
```xml
<ProjectReference Include="../EfCoreMigrations/EfCoreMigrations.csproj" />
```
They'll fail until your migration history produces the same schema, which
makes them a usable checklist for how far you've got. Running them needs
Docker (they spin up a throwaway Postgres container via Testcontainers, one
per test class) -- not the `docker compose` Postgres from step 1, a separate,
disposable one per test run.

### 6. Start Exercising
Open [EXERCISE.md](EXERCISE.md) and begin with **Part 1: The Initial Model
and First Migration**.

## What You've Learned So Far

If you've come from earlier modules in this repository, `Microsoft.EntityFrameworkCore`
has been present since module 03 -- but always as a supporting actor, never
the subject. This is the first place you'll create a migration by hand,
watch what it scaffolds, and fix one that gets something wrong.

## How to Approach Each Part

### Part 1: Initial Model
**Goal**: Get from "two C# classes" to "two real Postgres tables" and
understand every file `dotnet ef migrations add` creates.

**Watch for**: why `IDesignTimeDbContextFactory` exists, and what happens if
you delete it and try `migrations add` again.

### Part 2: Adding a Column
**Goal**: See the simplest possible migration -- one operation each way.

**Watch for**: nothing dramatic. That's the point -- purely additive changes
are supposed to be boring.

### Part 3: Renaming a Column
**Goal**: Internalize that `DropColumn`+`AddColumn` and `RenameColumn` are
not interchangeable, even though they can look similar in a migration diff.

**Watch for**: whether your own `dotnet ef` auto-detects the rename. Either
way, read the generated file before you run `database update` -- that habit
is the actual lesson, not which outcome you happened to get.

### Part 4: Index and Check Constraint
**Goal**: Add a database-enforced constraint through the Fluent API and see
Postgres actually reject bad data because of it.

**Watch for**: `ToTable(t => t.HasCheckConstraint(...))` generating a real,
reversible `AddCheckConstraint` migration operation -- no raw SQL needed.

### Part 5: Two Kinds of Seed Data
**Goal**: Know which tool to reach for -- `HasData` or a runtime seed --
before you've written the wrong one and had to undo it.

**Watch for**: the `InsertData` operation `HasData` generates baking the
actual row values into the migration file itself.

### Part 6: Applying and Rolling Back
**Goal**: Comfort with `database update <migration>` and `migrations remove`
as everyday tools, not emergency procedures.

**Watch for**: `migrations list` marking migrations "(pending)" after a
rollback, and clearing again once you roll forward.

## Troubleshooting

### "Unable to connect to database" / `dotnet ef database update` fails
- Is Postgres running? `docker compose ps` from `10-EntityFrameworkCore/`
- Does `appsettings.json`'s connection string match? Default is
  `Host=localhost;Port=5432;Database=efcore_migrations;Username=postgres;Password=postgres`
- Restart it: `docker compose down && docker compose up -d`

### "No database provider has been configured for this DbContext" (design time)
- Check `AppDbContextFactory` implements `IDesignTimeDbContextFactory<AppDbContext>`
  and calls `.UseNpgsql(...)` on the options builder
- Make sure it's a public, non-abstract class somewhere in your project

### "Unable to create an object of type 'AppDbContext'"
- `dotnet ef` couldn't find your factory or your `DbContext`. Confirm the
  project builds (`dotnet build`) -- `dotnet ef` builds your project first
  and silently can't proceed if that fails
- Run with `--verbose` to see exactly what `dotnet ef` tried:
  `dotnet ef migrations add Foo --verbose`

### Generated migration is empty
- You ran `migrations add` without changing the model since the last one.
  Remove it (`dotnet ef migrations remove`) and make sure your model change
  actually saved

### `dotnet ef migrations remove` refuses to run
- The migration you're trying to remove has already been applied to the
  database `dotnet ef` can see. Roll back first:
  `dotnet ef database update <PreviousMigrationName>`, then remove

### Check constraint doesn't seem to be enforced
- Did you apply the migration? `dotnet ef database update`
- Query it directly: `SELECT conname, pg_get_constraintdef(oid) FROM
  pg_constraint WHERE conrelid = 'books'::regclass;`

## Key Concepts to Internalize

### 1. A Migration Is Generated Code, Not a Black Box
Every `dotnet ef migrations add` output is a plain C# file. Read it before
you trust it, especially anything touching a rename.

### 2. `Up` and `Down` Are Both Your Responsibility
A migration with an incorrect or missing `Down` method isn't reversible no
matter what the tool's default template implies.

### 3. The Right Seeding Tool Depends on the Data's Lifecycle
Fixed and versioned → `HasData`. Independent of the schema's history and
possibly volatile → a runtime seed.

### 4. Design Time and Run Time Are Different Processes
`dotnet ef` doesn't run your app. `IDesignTimeDbContextFactory` is the
explicit bridge between the two.

## After Completing This Module

You'll understand:
- What actually happens, statement by statement, when you run
  `dotnet ef database update`
- Why some migrations are safe to write by hand and some are not
- The tradeoffs between the three ways to get a migration applied to a real
  environment

You'll be able to:
- Create, inspect, apply, and roll back migrations confidently
- Recognize a dangerous rename before it reaches production
- Add indexes and check constraints the idiomatic EF Core 7+ way
- Choose `HasData` or a runtime seed correctly for new data you're asked to
  add

## Next Steps

1. Complete all 6 parts of `EXERCISE.md`
2. Run `dotnet test ../tests` against your own workspace project (see
   "How to Use the Reference Solution" above) and get every test green
3. Move to **[EfCoreQuerying](../EfCoreQuerying/)** and query the schema you
   just built

---

**Ready to start?** Open [EXERCISE.md](EXERCISE.md) and begin with Part 1!
