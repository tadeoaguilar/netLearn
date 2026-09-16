# Module 13: Graph Database with Neo4j

## Overview
Modules 10 and 11 covered two data models — relational (PostgreSQL) and
document (Cosmos DB). This module covers the third: graph. The difference
isn't cosmetic. In a graph database, a relationship isn't a foreign key
column or a join table or an embedded array — it's data in its own right,
with properties, that the query engine can traverse in constant time per
hop regardless of how large the graph gets. Questions that are awkward or
slow in the other two models ("who are my mutual connections with this
person," "what's the shortest path between these two people," "who's the
most influential node in this network") are what a graph database is
actually good at, and this module is built around asking exactly those
questions of a small, deliberately relationship-heavy dataset.

This module is **Neo4j-only**. Cosmos DB does have a Gremlin (graph) API,
but its Docker/Linux emulator does not support Gremlin at all — only a
legacy Windows-only emulator ever did — so there's no free local dev story
for it in this repo. Neo4j is also simply the industry-standard graph
database: Cypher, the best-supported .NET driver, and a real graph
algorithms library.

## Learning Objectives
- Model a domain as nodes, relationships, and properties, and know when
  that's the right call over the relational or document models from
  modules 10-11
- Query a graph with Cypher through the official .NET driver, safely
  (parameterized, never string-concatenated)
- Traverse relationships — shortest paths, variable-length patterns, mutual
  connections — and understand why a graph database answers these
  differently than a relational or document one
- Run real graph analytics with the Graph Data Science library: PageRank,
  community detection, similarity
- Use Neo4j's ACID transactions, which (unlike Cosmos DB's
  `TransactionalBatch`) span any nodes and relationships, not one
  partition key

## Prerequisites
- .NET 9.0 SDK (pinned in `global.json`)
- **Docker**, to run Neo4j for these exercises (and for the
  Testcontainers-based integration tests — see below)
- Module 10 or 11 as a point of comparison is useful but not required —
  several exercises here explicitly contrast with both

## Running Neo4j for this module
Unlike module 10's single shared Postgres container, **Neo4j Community
Edition only supports one database per instance** — there's no multi-
database trick available here. Instead, `docker-compose.yml` at this
module's root starts **five separate Neo4j containers**, one per project:

```bash
cd 13-GraphDatabaseNeo4j
docker compose up -d
```

| Project | Neo4j Browser | Bolt (driver) | Credentials |
|---|---|---|---|
| GraphModeling | http://localhost:7474 | `bolt://localhost:7687` | `neo4j` / `graphmodeling` |
| GraphQuerying | http://localhost:7475 | `bolt://localhost:7688` | `neo4j` / `graphquerying` |
| GraphTraversals | http://localhost:7476 | `bolt://localhost:7689` | `neo4j` / `graphtraversals` |
| GraphAlgorithms | http://localhost:7477 | `bolt://localhost:7690` | `neo4j` / `graphalgorithms` |
| GraphTransactions | http://localhost:7478 | `bolt://localhost:7691` | `neo4j` / `graphtransactions` |

Each project's `solution/appsettings.json` already points at its own
container. `GraphAlgorithms`'s container additionally loads the Graph Data
Science plugin, which takes a little longer to start the first time.
Stop everything with `docker compose down`, or `docker compose down -v` to
also wipe all five data volumes.

**Integration tests use [Testcontainers](https://testcontainers.com/)
instead** — each test run starts its own throwaway Neo4j container, so
`dotnet test` needs Docker running but not `docker compose up` first.

## Projects

### GraphModeling
**What you'll learn:**
- Nodes, relationship types, and properties as first-class data — not
  columns, not join tables, not embedded documents
- Labels vs. relationship types, and designing directionality on purpose
- Uniqueness constraints and property indexes
- The shared domain's schema: `Person`, `Company`, `KNOWS`, `WORKS_AT`,
  `FOLLOWS`

### GraphQuerying
**What you'll learn:**
- Cypher fundamentals through `Neo4j.Driver`'s `IAsyncSession` and
  `ExecuteReadAsync`/`ExecuteWriteAsync`
- Parameterized queries — the Cypher-injection risk of string-concatenated
  queries, same lesson as modules 10/11's raw-SQL/SQL-API warnings
- Pattern matching, variable-length relationships, aggregation, pagination

### GraphTraversals
**What you'll learn:**
- `shortestPath()`/`allShortestPaths()` and controlling traversal depth
- Mutual-connection and degrees-of-separation queries
- Why these are a few milliseconds in a graph database and a recursive CTE
  (or worse) in a relational one

### GraphAlgorithms
**What you'll learn:**
- The Graph Data Science library: projecting an in-memory named graph
- PageRank (the most influential person in the network)
- Louvain community detection (finding friend groups)
- A similarity algorithm (Jaccard — "people you may know")

### GraphTransactions
**What you'll learn:**
- Explicit multi-statement transactions via the driver
- A referral scenario spanning a relationship write and two property
  updates, and proving rollback on a simulated failure
- Why this is straightforward in Neo4j and structurally impossible in
  Cosmos DB's partition-scoped `TransactionalBatch`

## Running This Module

```bash
# From the repository root
dotnet build netLearn.sln          # all projects in the module
dotnet test netLearn.sln           # all tests in the module (needs Docker)
```

| Project | Run the reference | Run the tests |
|---|---|---|
| GraphModeling | `dotnet run --project 13-GraphDatabaseNeo4j/GraphModeling/solution` | `dotnet test 13-GraphDatabaseNeo4j/GraphModeling/tests` (Docker) |
| GraphQuerying | `dotnet run --project 13-GraphDatabaseNeo4j/GraphQuerying/solution` | `dotnet test 13-GraphDatabaseNeo4j/GraphQuerying/tests` (Docker) |
| GraphTraversals | `dotnet run --project 13-GraphDatabaseNeo4j/GraphTraversals/solution` | `dotnet test 13-GraphDatabaseNeo4j/GraphTraversals/tests` (Docker) |
| GraphAlgorithms | `dotnet run --project 13-GraphDatabaseNeo4j/GraphAlgorithms/solution` | `dotnet test 13-GraphDatabaseNeo4j/GraphAlgorithms/tests` (Docker) |
| GraphTransactions | `dotnet run --project 13-GraphDatabaseNeo4j/GraphTransactions/solution` | `dotnet test 13-GraphDatabaseNeo4j/GraphTransactions/tests` (Docker) |

## Getting Started
Start `docker compose up -d` in this folder, then start with
[GraphModeling](GraphModeling/) and progress sequentially — the later
projects assume you're comfortable with the shared domain from the first
one.

## Next Module
This is currently the last module. Compare
[GraphTransactions](GraphTransactions/) against
[11-NoSqlCosmosDb/CosmosConsistencyAndTransactions](../11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/)
and [10-EntityFrameworkCore/EfCoreTransactions](../10-EntityFrameworkCore/EfCoreTransactions/)
— the same underlying problem, three genuinely different sets of
guarantees.
