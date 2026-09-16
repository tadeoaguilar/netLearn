# GraphAlgorithms - Neo4j Graph Data Science

## Overview
This project teaches the Neo4j **Graph Data Science (GDS)** library through hands-on exercises. You'll seed a small professional network shaped to have real structure, then run three whole-graph algorithms against it: PageRank (influence), Louvain (community detection), and Node Similarity ("people you may know").

This is the one project in the `13-GraphDatabaseNeo4j` module that uses GDS -- the other four (`GraphModeling`, `GraphQuerying`, `GraphTraversals`, `GraphTransactions`) work with plain Cypher.

## What You'll Learn

### Core Concepts
- **Projection vs. live traversal**: why GDS algorithms run against an in-memory snapshot instead of the transactional graph directly
- **The GDS graph catalog**: projecting a named graph, its independent lifecycle, and dropping it
- **PageRank**: ranking nodes by recursive influence, not raw in-degree
- **Louvain**: detecting communities by modularity, without specifying cluster count up front
- **Node Similarity**: Jaccard-coefficient recommendations from shared relationships

### Practical Skills
- Writing idempotent seed data specifically shaped for algorithms to find something meaningful
- Calling GDS procedures (`gds.graph.project`, `gds.pageRank.stream`, `gds.louvain.stream`, `gds.nodeSimilarity.stream`) from the Neo4j .NET driver
- Joining GDS's numeric node IDs back to domain data via `gds.util.asNode`
- Separating GDS-dependent code from pure, unit-testable result-shaping logic

## Project Structure

```
GraphAlgorithms/
├── GraphAlgorithms/            # Your workspace -- follow EXERCISE.md here
│   ├── GraphAlgorithms.csproj
│   ├── Program.cs              # Starting stub
│   └── appsettings.json        # Points at bolt://localhost:7690
├── solution/                   # Full reference implementation
│   ├── GraphAlgorithms.Solution.csproj
│   ├── Program.cs              # Orchestrates every part of the exercise
│   ├── appsettings.json
│   ├── Seeding/
│   │   └── GraphSeeder.cs      # Idempotent seed data, deliberately structured
│   ├── Gds/
│   │   ├── GraphNames.cs       # Catalog graph name constants
│   │   ├── CypherQueries.cs    # Pure query-text builders (unit tested)
│   │   └── GraphCatalog.cs     # Project/drop/exists against a live session
│   ├── Algorithms/
│   │   ├── PageRankRunner.cs
│   │   ├── LouvainRunner.cs
│   │   └── NodeSimilarityRunner.cs
│   ├── Models/                 # PersonInfluence, CommunityMembership, SimilarityPair
│   └── Results/
│       └── ResultFormatting.cs # Pure sorting/grouping/filtering (unit tested)
├── tests/
│   ├── GraphAlgorithms.Tests.csproj
│   ├── ResultFormattingTests.cs  # Pure unit tests (no Docker needed)
│   ├── CypherQueriesTests.cs     # Pure unit tests (no Docker needed)
│   ├── GdsFixture.cs             # Shared Testcontainers + GDS plugin fixture
│   └── GdsAlgorithmTests.cs      # Integration tests (need Docker + GDS)
├── EXERCISE.md                  # Step-by-step exercise guide
├── GETTING_STARTED.md            # Quick start instructions
└── README.md                     # This file
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK (already installed)
- Docker (for the Neo4j + GDS container and the integration tests)
- Basic familiarity with Cypher (from `GraphModeling`/`GraphQuerying`) is assumed

### Quick Start

1. **Start the container** (from the `13-GraphDatabaseNeo4j` folder):
   ```bash
   docker compose up -d neo4j-algorithms
   ```
   This container is slower to become ready than the module's other four -- it has to download and load the Graph Data Science plugin. Give it a minute or two before connecting.

2. **Verify GDS actually loaded**, either in Neo4j Browser (`http://localhost:7477`, login `neo4j` / `graphalgorithms`) or with a quick query:
   ```cypher
   RETURN gds.version();
   ```
   If that errors instead of returning a version string, the plugin hasn't finished loading yet (or didn't load) -- check `docker logs netlearn-graphalgorithms-neo4j`.

3. **Navigate to the project:**
   ```bash
   cd 13-GraphDatabaseNeo4j/GraphAlgorithms/GraphAlgorithms
   ```

4. **Verify the workspace builds:**
   ```bash
   dotnet build
   ```

5. **Read the getting started guide:**
   Open [GETTING_STARTED.md](GETTING_STARTED.md)

6. **Start the exercise:**
   Open [EXERCISE.md](EXERCISE.md) and follow Part 1

### The Learning Path

1. **Part 1: Why GDS is different** (15 minutes) -- projection vs. live traversal, confirm GDS is loaded
2. **Part 2: Projecting a named graph** (30 minutes) -- seed data, project on FOLLOWS, understand the catalog lifecycle
3. **Part 3: PageRank** (25 minutes) -- rank people by influence, not follower count
4. **Part 4: Louvain** (25 minutes) -- project on KNOWS, detect friend-group communities
5. **Part 5: Node Similarity** (20 minutes) -- "people you may know" via Jaccard similarity

**Total Time**: ~2 hours

## Reference Implementation (`solution/`)

The `solution/` project is a complete, buildable answer key. Run it once you've attempted the exercise yourself:

```bash
cd 13-GraphDatabaseNeo4j/GraphAlgorithms/solution
dotnet run
```

It seeds the graph, prints the GDS version, runs all three algorithms, and cleans up its projections afterward.

## Tests (`tests/`)

```bash
cd 13-GraphDatabaseNeo4j/GraphAlgorithms/tests
dotnet test
```

Two kinds of tests live here:
- **Unit tests** (`ResultFormattingTests`, `CypherQueriesTests`) -- pure logic, no Neo4j or Docker required. Run these with `dotnet test --filter Category=Unit` if you don't have Docker running.
- **Integration tests** (`GdsAlgorithmTests`) -- spin up a real `neo4j:5-community` Testcontainer with the GDS plugin loaded (`NEO4J_PLUGINS=["graph-data-science"]`), seed it, and assert the algorithms found what the seed data was designed to produce: Alice ranks highest in PageRank, Louvain recovers at least three communities, and Bob/Carol (identical follow-sets) score higher in Node Similarity than Bob/Mallory (disjoint follow-sets). Because the container needs the GDS plugin loaded, it takes noticeably longer to start than the plain containers in the other four projects' test suites -- the fixture uses a 5-minute startup budget rather than Testcontainers' default.

> **Note:** these integration tests need a working Docker daemon and were not executed in the environment this project was built in (no Docker available there). They compile and are written to the exact behavior the seed data guarantees; the unit tests, which need no Docker, were run and pass.

## Key Takeaways

After completing this project, you should be able to:

- Explain why GDS projects a graph before computing, instead of running algorithms live
- Project a named graph, understand its catalog lifecycle, and drop it when done
- Run PageRank and explain why it's not the same as counting followers
- Run Louvain and explain what modularity-based community detection is doing
- Run Node Similarity and explain the Jaccard coefficient it's computing
- Separate GDS-dependent code (needs a live session) from pure result-shaping logic (unit-testable without one)

## Common Mistakes to Avoid

- Forgetting to project a graph before calling an algorithm (`gds.pageRank.stream` etc. need an existing catalog entry, not raw node/relationship labels)
- Writing more data after projecting and expecting the algorithm to see it without re-projecting
- Using `orientation: 'NATURAL'` for a symmetric relationship like `KNOWS` (Louvain wants `UNDIRECTED` there) or vice versa
- Assuming PageRank score ranks the same as raw in-degree count
- Forgetting `gds.graph.drop(graphName, false)` (the `false` makes a missing-graph drop a no-op instead of an error) and leaking projections across runs

## Next Steps

This is the last project in `13-GraphDatabaseNeo4j`. Continue to whichever module comes next in your learning path, or revisit the module's other four projects if you want more Cypher practice before moving on.

## Additional Resources

- [Neo4j Graph Data Science Documentation](https://neo4j.com/docs/graph-data-science/current/)
- [GDS Algorithms Reference](https://neo4j.com/docs/graph-data-science/current/algorithms/)
- [PageRank](https://neo4j.com/docs/graph-data-science/current/algorithms/page-rank/)
- [Louvain](https://neo4j.com/docs/graph-data-science/current/algorithms/louvain/)
- [Node Similarity](https://neo4j.com/docs/graph-data-science/current/algorithms/node-similarity/)

## Tips for Success

1. **Type the code yourself** - Don't copy-paste. Muscle memory helps learning.
2. **Watch the container start** - `docker logs -f netlearn-graphalgorithms-neo4j` while it comes up, so you can see the plugin load rather than wondering why the driver can't connect yet.
3. **Run the pure unit tests early** - they need no Docker and confirm your seed data's shape (via `ResultFormattingTests`) is sound before you touch the container.
4. **Print intermediate results** - after each `YIELD`, print the raw rows before shaping them, so you can see exactly what GDS handed back.
5. **Ask questions** - If something doesn't make sense, investigate or ask for help.
