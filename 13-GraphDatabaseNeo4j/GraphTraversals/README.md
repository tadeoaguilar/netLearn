# GraphTraversals - Shortest Paths, Bounded Depth, and Degrees of Separation

## Overview
This project teaches you Neo4j's traversal-oriented Cypher features through a hands-on exercise: finding the shortest path between two people, finding *every* shortest path when there's a tie, bounding how far a variable-length pattern is allowed to search, and answering "who's connected to whom, and by how much" questions that get painful fast in a relational or document database.

You'll work against a small but deliberately-shaped professional-network graph -- people who KNOW and FOLLOW each other, and WORK_AT companies -- seeded with hubs, an "island" cluster, and a pair of people who are connected only through several intermediaries.

## What You'll Learn

### Core Concepts
- **`shortestPath()`**: the single shortest path between two nodes, returned as an actual path you can read node-by-node
- **`allShortestPaths()`**: every path tied for shortest, not just one of them
- **Bounded variable-length patterns**: `-[:KNOWS*1..4]-` instead of an unbounded `-[:KNOWS*]-`, and why that bound matters on a real graph
- **Undirected vs. directed matching**: `-[:KNOWS]-` (no arrow) vs. `-[:FOLLOWS]->` (arrow) in the same style of query
- **Mutual connections**: a two-hop pattern that answers "who do these two people both know"
- **Degrees of separation**: grouping everyone reachable from a person by their exact hop distance, and why this is the kind of query graph databases exist for

### Practical Skills
- Writing Cypher path patterns that return the path, not just a boolean or a count
- Reasoning about when a query needs `allShortestPaths()` instead of `shortestPath()`
- Bounding traversal depth to keep a query's cost proportional to what you actually need
- Comparing how "how are these two things connected" is answered across relational, document, and graph models

## Project Structure

```
GraphTraversals/
├── GraphTraversals/            # Your workspace -- start here
│   ├── GraphTraversals.csproj
│   ├── Program.cs              # Follow EXERCISE.md to fill this in
│   └── appsettings.json        # bolt://localhost:7689, neo4j/graphtraversals
├── solution/                   # Reference implementation (don't peek until you're stuck)
│   ├── GraphTraversals.Solution.csproj
│   ├── Program.cs
│   ├── GraphSeeder.cs          # Builds the sample graph (idempotent)
│   ├── TraversalQueries.cs     # ShortestPathAsync, AllShortestPathsAsync, ...
│   └── appsettings.json
├── tests/                      # Real, Testcontainers-backed tests against the solution
│   ├── GraphTraversals.Tests.csproj
│   ├── Neo4jFixture.cs         # Shared container + seed data for the whole test run
│   ├── SeedTests.cs
│   ├── ShortestPathTests.cs
│   ├── AllShortestPathsTests.cs
│   ├── DepthBoundedTraversalTests.cs
│   ├── MutualConnectionsTests.cs
│   ├── DegreesOfSeparationTests.cs
│   └── FollowsDirectionTests.cs
├── EXERCISE.md                 # Step-by-step exercise guide
├── GETTING_STARTED.md          # Quick start instructions
└── README.md                   # This file
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK (already installed)
- Docker (to run Neo4j locally via `docker compose`, and for the Testcontainers-backed tests)
- Basic Cypher familiarity (module 13's `GraphModeling` and `GraphQuerying` projects, if you haven't done them yet, cover the fundamentals this project builds on)

### Quick Start

1. **Start Neo4j for this project:**
   ```bash
   cd 13-GraphDatabaseNeo4j
   docker compose up -d neo4j-traversals
   ```

2. **Navigate to your workspace:**
   ```bash
   cd GraphTraversals/GraphTraversals
   ```

3. **Verify setup:**
   ```bash
   dotnet build
   ```

4. **Read the getting started guide:**
   Open [GETTING_STARTED.md](GETTING_STARTED.md)

5. **Start the exercise:**
   Open [EXERCISE.md](EXERCISE.md) and follow Part 1

### The Sample Graph

`GraphSeeder` (in `solution/GraphSeeder.cs`) builds a 14-person network:

- **Hubs**: Alice and Bob each directly KNOW five other people.
- **A multi-hop pair**: Erin and Ivan don't know each other, and don't share a mutual friend either -- the only way from one to the other is `erin -> alice -> bob -> ivan`, three hops.
- **A tied shortest path**: Carol and Grace are two hops apart in exactly two different ways -- through Bob, and through Judy.
- **A loosely-attached island**: Mallory, Niaj, and Olivia know each other, but the *only* edge connecting that trio to everyone else is `judy -> mallory`. Peggy hangs off Olivia alone, five hops from Alice -- far enough that a depth-bounded `*1..3` query must not reach her.

The full edge lists (with a comment explaining the intent of each part of the graph) are in `solution/GraphSeeder.cs`.

## Key Takeaways

After completing this project, you should be able to:

✅ Write a Cypher query that returns an actual shortest path, and read the intermediate nodes off of it
✅ Explain when you need `allShortestPaths()` instead of `shortestPath()`
✅ Bound a variable-length relationship pattern and explain why an unbounded one is dangerous on a large graph
✅ Choose undirected vs. directed matching based on what a relationship actually means, not just how it happens to have been created
✅ Write a mutual-connections query and a degrees-of-separation query from scratch
✅ Explain why "how far apart are these two nodes, and who's in between" gets dramatically harder in Postgres and Cosmos DB as the number of hops grows

## Testing Your Understanding

After completing the exercises, try to answer:

1. If `shortestPath()` and `allShortestPaths()` can both find "the" shortest path, why does a separate function for "all of them" exist at all?
2. What's the actual risk of writing `-[:KNOWS*]-` with no upper bound in a query against a graph with millions of people?
3. Why does modeling `KNOWS` as one directed relationship but always *querying* it as undirected make sense, instead of just creating it twice (once in each direction)?
4. Why does a recursive CTE in Postgres get more expensive with every additional hop, when a Cypher traversal doesn't slow down the same way?

## Next Steps

This is the third of five Neo4j projects in module 13. If you haven't already:

- **GraphModeling** -- nodes, relationships, properties, and constraints
- **GraphQuerying** -- filtering, aggregation, and pattern matching fundamentals

come before this one. After GraphTraversals:

- **GraphAlgorithms** -- PageRank, community detection, and the Graph Data Science library
- **GraphTransactions** -- transactional writes and consistency in Neo4j

## Tips for Success

1. **Type the code yourself** - Don't copy-paste. Muscle memory helps learning.
2. **Run the queries in Neo4j Browser too** - `http://localhost:7476`, credentials `neo4j`/`graphtraversals`. Seeing the graph drawn out makes the hop-distance reasoning much more concrete than reading a result table.
3. **Predict before you run** - For each query in EXERCISE.md, work out on paper what you expect back, given the graph shape described above, before running it.
4. **Ask questions** - If a result surprises you, that's usually a sign your mental model of the graph (or of the query) is off by one hop somewhere -- worth chasing down.

---

**Ready to begin?** Open [GETTING_STARTED.md](GETTING_STARTED.md) to start your journey!
