# Module 11: NoSQL with Azure Cosmos DB and .NET Aspire

## Overview
Module 10 taught EF Core against real PostgreSQL. This module is the NoSQL
counterpart, on Azure Cosmos DB's document (NoSQL) API — and it's a
deliberate contrast, not just a different database. A relational schema has
one shape enforced by the database; a Cosmos container holds documents of
whatever shape your code writes. A relational query planner picks an index
for you; Cosmos charges you Request Units per query and it's your job to
keep that cost down. A relational transaction spans however many tables you
touch; a Cosmos transaction is scoped to a single partition key, full stop.
Every exercise here exists to make one of those differences concrete.

Orchestration is **.NET Aspire**, not Docker Compose — this module's second
teaching goal. Each project has its own small `AppHost` (the same pattern as
`09-EnterpriseCRUD`'s `TaskManagement.AppHost`) that starts the Cosmos DB
Linux emulator in a container and wires its connection info into the app.
Like module 09, Aspire here is orchestration, not a dependency: every
project still runs standalone against a manually-started emulator if you'd
rather skip Aspire for a given run.

## Learning Objectives
- Model documents and choose a partition key, and explain the trade-offs
  versus the normalized, foreign-keyed schemas from module 10
- Query with both the Cosmos LINQ provider and the parameterized SQL API,
  and understand cross-partition query cost
- Read and control Request Unit (RU) cost: indexing policy, projections,
  throughput provisioning, and handling `429` responses
- Build a change feed processor and a materialized read model
- Choose a consistency level deliberately, and use ETags and
  `TransactionalBatch` for concurrency and atomicity within a partition
- Orchestrate a local dependency with .NET Aspire: an `AppHost`, an emulator
  resource, and Aspire-based integration tests

## Prerequisites
- .NET 9.0 SDK (pinned in `global.json`)
- **Docker**, to run the Cosmos DB emulator via Aspire (or by hand) and for
  the Aspire-based integration tests. The emulator is a heavier container
  than module 10's Postgres image — expect a multi-minute first start and
  real memory use (a few GB).
- Module 10 is a useful (not required) point of comparison — several
  exercises here explicitly call back to it

## Running the Cosmos DB emulator
Each project has its own `AppHost/` — that's the point of this module's
Aspire lesson, so there's no single shared compose file this time:

```bash
dotnet run --project 11-NoSqlCosmosDb/CosmosModeling/AppHost
```

This starts the Cosmos DB emulator in a container, waits for it to be ready,
and launches that project's `solution/` wired to it. The Aspire dashboard
(the AppHost prints its URL on startup) shows the emulator resource, logs,
and traces.

**Prefer to skip Aspire for a run?** Start the emulator yourself and the
`solution`/workspace projects work unmodified — their `appsettings.json`
already points at `https://localhost:8081` with the emulator's well-known
default key (public, documented by Microsoft, never a real secret; it only
ever authenticates against the local emulator):

```bash
docker run -p 8081:8081 -p 10250-10255:10250-10255 --name cosmos-emulator \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```

**Integration tests use `Aspire.Hosting.Testing`** — `dotnet test` boots the
same `AppHost` project (emulator included) in-process, so you don't need to
`dotnet run` the AppHost first. You do still need Docker running.

## Projects

### CosmosModeling
**What you'll learn:**
- Choosing a partition key (`/customerId`) for distribution and query
  locality, versus module 10's foreign keys
- Embedding (`Order.OrderLines`) vs. referencing a separate container
- Schema evolution without migrations — handling old and new document
  shapes directly in code
- Synthetic/composite partition keys for higher cardinality

**Domain:** retail orders — a `Customer` container and an `Order` container
with embedded order lines.

### CosmosQuerying
**What you'll learn:**
- The LINQ provider (`GetItemLinqQueryable<T>`) and the parameterized SQL
  API (`QueryDefinition`)
- Cross-partition query cost, and querying within a single partition
- Pagination with `FeedIterator` and continuation tokens
- Projections to cut RU cost, not just payload size

**Domain:** the same orders/customers shape, seeded with ~150-200 orders.

### CosmosIndexingAndThroughput
**What you'll learn:**
- Customizing the indexing policy: excluded paths, composite indexes
- Reading `RequestCharge` on every response
- Manual vs. autoscale throughput at the container level
- Handling `429 TooManyRequests` and `RetryAfter`

**Domain:** same shape, focused on read/write cost rather than new entities.

### CosmosChangeFeed
**What you'll learn:**
- The change feed processor and its lease container
- Building a materialized read model kept in sync as documents change
- At-least-once delivery and idempotent change handlers

**Domain:** a `CustomerOrderSummary` read model derived from `Order` changes.

### CosmosConsistencyAndTransactions
**What you'll learn:**
- The five consistency levels, and setting them at the client vs. per-request
- ETag-based optimistic concurrency — Cosmos's version of module 10's `xmin`
- `TransactionalBatch` — atomic multi-item operations within one partition
  key, and why Cosmos has no cross-partition transaction

**Domain:** same shape, focused on concurrent/atomic operations.

## Running This Module

```bash
# From the repository root
dotnet build netLearn.sln          # all projects in the module (no Docker needed to build)
dotnet test netLearn.sln           # all tests (module 11's need Docker + the emulator)
```

| Project | Run via Aspire | Run the tests |
|---|---|---|
| CosmosModeling | `dotnet run --project 11-NoSqlCosmosDb/CosmosModeling/AppHost` | `dotnet test 11-NoSqlCosmosDb/CosmosModeling/tests` (Docker) |
| CosmosQuerying | `dotnet run --project 11-NoSqlCosmosDb/CosmosQuerying/AppHost` | `dotnet test 11-NoSqlCosmosDb/CosmosQuerying/tests` (Docker) |
| CosmosIndexingAndThroughput | `dotnet run --project 11-NoSqlCosmosDb/CosmosIndexingAndThroughput/AppHost` | `dotnet test 11-NoSqlCosmosDb/CosmosIndexingAndThroughput/tests` (Docker) |
| CosmosChangeFeed | `dotnet run --project 11-NoSqlCosmosDb/CosmosChangeFeed/AppHost` | `dotnet test 11-NoSqlCosmosDb/CosmosChangeFeed/tests` (Docker) |
| CosmosConsistencyAndTransactions | `dotnet run --project 11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/AppHost` | `dotnet test 11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/tests` (Docker) |

Each project holds a workspace where you write code, a `solution/` reference
implementation, an `AppHost/` for Aspire orchestration, and `tests/`. Work
the exercise first, then compare.

## Getting Started
Start with [CosmosModeling](CosmosModeling/) and progress sequentially — the
later projects assume you're comfortable with the domain shape and partition
key from the first one.

## Next Module
Continue to [12-AzureFunctionsServerless](../12-AzureFunctionsServerless/)
for serverless compute and Entra ID security, or
[13-GraphDatabaseNeo4j](../13-GraphDatabaseNeo4j/) for the third data model
this repo covers — graph, after relational (module 10) and document (this
one).

If you haven't already, it's also worth going back and comparing this
module's `CosmosConsistencyAndTransactions` project against
[10-EntityFrameworkCore/EfCoreTransactions](../10-EntityFrameworkCore/EfCoreTransactions/)
— same problem (concurrent writes, atomicity), two very different answers.
