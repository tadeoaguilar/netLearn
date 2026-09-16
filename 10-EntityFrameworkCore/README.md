# Module 10: Entity Framework Core with PostgreSQL

## Overview
Entity Framework Core is the data-access layer most of this repository has
been building on top of since module 03, but none of the earlier modules
teach it directly. This module does — end to end, against real PostgreSQL,
not a SQLite stand-in. You'll model a schema, evolve it with migrations,
query it with LINQ, wrap changes in transactions, watch what EF Core sends
to the database, and expose its health to the outside world.

Unlike modules 03-09, this module is **PostgreSQL-only**. Several exercises
(array columns, JSONB, `ILIKE`, serializable transactions, the `xmin`
concurrency token) rely on Postgres-specific behavior that SQLite can't
reproduce, which is exactly why they're worth learning on the real thing.

## Learning Objectives
- Model entities and relationships with the Fluent API, including owned
  types, value converters, explicit many-to-many, and inheritance
- Manage schema change over time with EF Core migrations, including hand
  edits, seeding, and rollback
- Write LINQ queries that translate well to SQL, and know when they don't
- Use PostgreSQL-specific querying features EF Core exposes: `ILIKE`, array
  columns, and JSONB
- Control transactions and concurrency explicitly, including Postgres
  isolation levels and optimistic concurrency
- Turn on EF Core's logging and hook interceptors into the pipeline for
  auditing and diagnostics
- Wire EF Core into ASP.NET Core health checks for readiness/liveness probes

## Prerequisites
- .NET 9.0 SDK (pinned in `global.json`)
- **Docker**, to run PostgreSQL for these exercises (and for the
  Testcontainers-based integration tests — see below)
- Modules 01 and 03 are a good warm-up (DI container basics, layering) but
  aren't required

## Running PostgreSQL for this module
A single `docker-compose.yml` at the root of this module starts one Postgres
container and provisions one database per project, so you only need to start
it once:

```bash
cd 10-EntityFrameworkCore
docker compose up -d
```

This creates `efcore_modeling`, `efcore_migrations`, `efcore_querying`,
`efcore_transactions` and `efcore_healthchecks` databases on `localhost:5432`
(user `postgres`, password `postgres` — local development only, never use
these credentials anywhere real). Each project's `solution/appsettings.json`
already points at its own database. Stop it with `docker compose down`, or
`docker compose down -v` to also wipe the data volume and start clean.

**Integration tests use [Testcontainers](https://testcontainers.com/) instead
of this container** — each test run starts its own throwaway Postgres, so
`dotnet test` needs Docker running but not `docker compose up` first. The one
exception is `EfCoreModeling`, whose tests only inspect EF Core's in-memory
model metadata and need no database at all.

## Projects

### EfCoreModeling
**What you'll learn:**
- Configuring entities with `IEntityTypeConfiguration<T>` and the Fluent API
- One-to-many relationships, an owned type (value object), and a value
  converter
- Explicit many-to-many with a payload on the join entity
- Table-per-hierarchy inheritance
- Indexes, unique constraints, and check constraints
- Mapping the whole schema to snake_case, the PostgreSQL convention

**Domain:** a small library catalog — `Author`, `Publisher`, `Book`,
`Genre`, `Review`, and a `DigitalBook` that inherits from `Book`.

### EfCoreMigrations
**What you'll learn:**
- Creating, applying, and rolling back migrations
- Hand-editing a generated migration to rename a column without losing data
- Adding an index and a check constraint via raw SQL inside a migration
- `HasData` seeding vs. an idempotent runtime seed
- `Database.Migrate()` vs. `dotnet ef database update` vs. migration bundles

**Domain:** a trimmed `Author`/`Book` schema, evolved step by step.

### EfCoreQuerying
**What you'll learn:**
- LINQ filtering, sorting, paging, and projections
- `Include`/`ThenInclude` vs. projecting to DTOs, `AsNoTracking`,
  `AsSplitQuery`
- `GroupBy` and aggregates, and a classic client-vs-server-evaluation gotcha
- Raw SQL with `FromSqlInterpolated`/`ExecuteSqlInterpolated`, mixed with LINQ
- PostgreSQL-only querying: `EF.Functions.ILike`, a native array column, and
  a JSONB column

**Domain:** the same library shape as `EfCoreModeling`, seeded with ~75
books so filters and aggregates have something to chew on.

### EfCoreTransactions
**What you'll learn:**
- Why a single `SaveChanges()` call is already a transaction
- Explicit transactions, commit/rollback, and savepoints
- Optimistic concurrency via Postgres's `xmin` system column
- Isolation levels — Read Committed vs. Serializable — and retrying a
  serialization failure
- A small unit-of-work wrapper

**Domain:** `Account`/`Transfer`, the classic bank-transfer example, because
it makes "what happens if this fails halfway through" concrete.

### EfCoreLoggingAndHealthChecks
**What you'll learn:**
- Simple logging (`LogTo`), sensitive data logging, and detailed errors —
  and why the last two are dev-only
- A `SaveChangesInterceptor` for audit timestamps and a
  `DbCommandInterceptor` that flags slow queries
- Wiring EF Core's logs through `ILoggerFactory` alongside app logs
- `AddDbContextCheck<T>` for a `/health/ready` endpoint vs. a
  no-database `/health/live` endpoint
- A custom health check that fails when there are unapplied migrations

**Domain:** a small `Product`/`Order` set, hosted in a minimal ASP.NET Core
API so the health endpoints have something to run in.

## Running This Module

```bash
# From the repository root
dotnet build netLearn.sln          # all projects in the module
dotnet test netLearn.sln           # all tests in the module (needs Docker)
```

Each project holds three things: a workspace where you write code, a
`solution/` folder with a reference implementation, and `tests/` proving the
behaviour. Work the exercise first, then compare.

| Project | Run the reference | Run the tests |
|---|---|---|
| EfCoreModeling | `dotnet run --project 10-EntityFrameworkCore/EfCoreModeling/solution` | `dotnet test 10-EntityFrameworkCore/EfCoreModeling/tests` |
| EfCoreMigrations | `dotnet run --project 10-EntityFrameworkCore/EfCoreMigrations/solution` | `dotnet test 10-EntityFrameworkCore/EfCoreMigrations/tests` (Docker) |
| EfCoreQuerying | `dotnet run --project 10-EntityFrameworkCore/EfCoreQuerying/solution` | `dotnet test 10-EntityFrameworkCore/EfCoreQuerying/tests` (Docker) |
| EfCoreTransactions | `dotnet run --project 10-EntityFrameworkCore/EfCoreTransactions/solution` | `dotnet test 10-EntityFrameworkCore/EfCoreTransactions/tests` (Docker) |
| EfCoreLoggingAndHealthChecks | `dotnet run --project 10-EntityFrameworkCore/EfCoreLoggingAndHealthChecks/solution` | `dotnet test 10-EntityFrameworkCore/EfCoreLoggingAndHealthChecks/tests` (Docker) |

## Getting Started
Start `docker compose up -d` in this folder, then start with
[EfCoreModeling](EfCoreModeling/) and progress sequentially — each later
project assumes you're comfortable with the modeling and migration basics
from the first two.

## Next Module
Continue to [11-NoSqlCosmosDb](../11-NoSqlCosmosDb/) for the NoSQL
counterpart to this module — Azure Cosmos DB orchestrated with .NET Aspire,
and a deliberate set of contrasts with the relational work you just did here
(partition keys instead of foreign keys, RU cost instead of query-plan cost,
transactions scoped to a partition instead of spanning tables).

Or, if you'd rather revisit module 09 first: go back to
[09-EnterpriseCRUD](../09-EnterpriseCRUD/) and notice how much of its
`TaskManagement.Infrastructure/Persistence` layer you can now explain from
first principles.
