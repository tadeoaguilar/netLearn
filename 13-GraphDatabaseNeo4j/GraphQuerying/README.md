# GraphQuerying - Querying a Graph with the Neo4j .NET Driver

## Overview
This project teaches you how to query a graph database through the Neo4j
.NET driver, using a small professional-network graph (people, companies,
and the relationships between them) as the running example. You'll write
parameterized Cypher, match patterns on node and relationship properties,
use variable-length relationships for multi-hop reachability, and aggregate
and page through results -- all against a real Neo4j instance.

## What You'll Learn

### Core Concepts
- **`IDriver`/`IAsyncSession`**: the Neo4j .NET driver's connection and
  session abstractions, and the `ExecuteReadAsync`/`ExecuteWriteAsync`
  read/write split
- **Parameterized Cypher**: why `$name` is safe and string-built Cypher is a
  Cypher-injection hole, the same failure mode as raw SQL or hand-built
  Cosmos SQL
- **Pattern matching**: node-property filters, relationship-property
  filters, multiple relationship types in one pattern, and when inline
  `{...}` matching reads better than a `WHERE` clause
- **Variable-length relationships**: `-[:FOLLOWS*1..3]->` for "everyone
  within N hops" in a single query
- **Aggregation and pagination**: `count()`, `collect()`, implicit grouping,
  and `WITH ... ORDER BY ... SKIP ... LIMIT`

### Practical Skills
- Seeding a graph idempotently with `UNWIND` + `MERGE`
- Reading Cypher results back into C# via `IRecord`
- Recognizing and avoiding Cypher injection
- Choosing the right pattern-matching style for a given filter
- Measuring how a reachable set grows with hop count
- Building stable, deterministic pagination over graph data

## Project Structure

```
GraphQuerying/
├── GraphQuerying/              # <- YOUR WORKSPACE. Write your code here.
│   ├── GraphQuerying.csproj
│   ├── Program.cs              #   replace as you work through the parts
│   └── appsettings.json        #   already points at this project's container
│
├── solution/                   # <- REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Domain/                 #   Person, Company
│   ├── Seed/                   #   GraphSeeder (deterministic, idempotent)
│   ├── Queries/                #   one demo class per exercise part
│   └── Program.cs
│
├── tests/                      # 23 tests, against a real Testcontainers Neo4j
│
├── EXERCISE.md                 # Step-by-step exercise guide
├── GETTING_STARTED.md          # Quick start instructions
└── README.md                   # This file
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK (already installed)
- Docker (for running Neo4j locally, and for the test suite's Testcontainers)
- Basic Cypher/graph-database familiarity is helpful but not required

### Quick Start

1. **Start this project's Neo4j container** (see `../docker-compose.yml`):
   ```bash
   docker compose -f ../docker-compose.yml up -d neo4j-querying
   ```

2. **Navigate to the project:**
   ```bash
   cd GraphQuerying
   ```

3. **Verify setup:**
   ```bash
   dotnet build
   ```

4. **Read the getting started guide:**
   Open [GETTING_STARTED.md](GETTING_STARTED.md)

5. **Start the exercise:**
   Open [EXERCISE.md](EXERCISE.md) and follow Part 0.

### The Learning Path

1. **Part 0: Domain Model, Seed Data, and Connecting** -- get a driver
   connection and a graph worth querying
2. **Part 1: The Driver's Read/Write Split** -- `ExecuteReadAsync` vs.
   `ExecuteWriteAsync`
3. **Part 2: Parameterized Queries** -- Cypher injection, and how `$name`
   prevents it
4. **Part 3: Pattern Matching** -- node/relationship property filters,
   multiple relationship types, inline vs. `WHERE`
5. **Part 4: Variable-Length Relationships** -- multi-hop reachability in
   one query
6. **Part 5: Aggregation and Pagination** -- `count()`, `collect()`,
   grouping, `SKIP`/`LIMIT`

## Key Takeaways

After completing this project, you should be able to:

- Explain the difference between `ExecuteReadAsync` and `ExecuteWriteAsync`,
  and why it matters even outside a cluster
- Write parameterized Cypher by default, and explain exactly why string
  interpolation into a query is unsafe
- Choose between inline pattern properties and a `WHERE` clause for a given
  filter
- Use `-[:REL*1..N]->` for bounded multi-hop reachability, and explain why
  the hop bound can't be a query parameter
- Aggregate and paginate Cypher results, including handling ties in a sort
  key

## Common Mistakes to Avoid

- Building a Cypher query string with `$"..."` interpolation instead of
  `$parameter` placeholders
- Returning an open `IResultCursor` from inside an
  `ExecuteReadAsync`/`ExecuteWriteAsync` delegate instead of consuming it
  there
- Forgetting that an undirected `MERGE`/`MATCH` (`-[:KNOWS]-`, no arrow)
  matches an edge from *both* endpoints, silently doubling naive counts
- Trying to parameterize a variable-length relationship's hop bound
  (`*1..$n`) -- it must be a literal
- Paginating by a non-unique sort key with no tiebreaker, making page
  boundaries unstable when values tie

## Next Steps

This module's other projects build on the same graph-database foundation:

- **GraphModeling** -- data modeling: nodes vs. relationships vs.
  properties, and schema/constraint design
- **GraphTraversals** -- traversal algorithms beyond simple variable-length
  paths
- **GraphAlgorithms** -- Graph Data Science library algorithms (PageRank,
  Louvain, node similarity)
- **GraphTransactions** -- explicit transactions and concurrency in Neo4j

## Additional Resources

- [Neo4j .NET Driver Manual](https://neo4j.com/docs/dotnet-manual/current/)
- [Cypher Manual](https://neo4j.com/docs/cypher-manual/current/)
- [Cypher Injection](https://neo4j.com/developer/kb/protecting-against-cypher-injection/)

## Tips for Success

1. **Type the code yourself** -- don't copy-paste. Muscle memory helps
   learning.
2. **Run Part 2's demo and actually look at both counts** -- reading about
   Cypher injection is not the same as watching `OR 1=1` leak every row.
3. **Run Part 4 at all three hop counts** -- the point is watching the
   reachable set grow, not just reading the final number.
4. **Ask questions** -- if something doesn't make sense, investigate or ask
   for help.

---

**Ready to begin?** Open [GETTING_STARTED.md](GETTING_STARTED.md) to start
your journey!
