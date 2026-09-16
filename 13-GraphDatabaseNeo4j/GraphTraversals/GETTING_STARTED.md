# Getting Started with GraphTraversals

## Quick Start Guide

### 1. Start Neo4j

This project's container is `neo4j-traversals`, defined in `13-GraphDatabaseNeo4j/docker-compose.yml`:

```bash
cd 13-GraphDatabaseNeo4j
docker compose up -d neo4j-traversals
```

It listens on `bolt://localhost:7689` (driver protocol) and `http://localhost:7476` (Neo4j Browser), with credentials `neo4j` / `graphtraversals`. Each of the five projects in this module gets its own container and port pair -- Neo4j Community Edition only supports one database per instance, so "one container per project" stands in for the multi-database trick earlier modules use.

Give it a few seconds to finish starting:

```bash
docker compose logs -f neo4j-traversals
# Ctrl+C once you see "Started."
```

### 2. Navigate to Your Workspace

```bash
cd 13-GraphDatabaseNeo4j/GraphTraversals/GraphTraversals
```

### 3. Verify the Project Setup

```bash
dotnet build
```

You should see a successful build message.

### 4. Project Structure

```
GraphTraversals/
├── GraphTraversals.csproj      # Project file with dependencies
├── Program.cs                  # Entry point (you'll fill this in)
├── appsettings.json            # Connection settings -- already pointed at port 7689
├── EXERCISE.md                 # Step-by-step exercise guide
└── GETTING_STARTED.md          # This file
```

### 5. Follow the Exercise

Open [EXERCISE.md](EXERCISE.md) and follow the step-by-step instructions. It's divided into five parts:

1. **Part 1**: `shortestPath()` -- the shortest KNOWS path between two people
2. **Part 2**: `allShortestPaths()` -- every path tied for shortest
3. **Part 3**: Bounding traversal depth and direction
4. **Part 4**: Mutual connections
5. **Part 5**: Degrees of separation

Each part tells you what code to write, why it matters, and leaves you a question or two to think through before moving on.

### 6. Running Your Code

After each part, run your code to see the results:

```bash
dotnet run
```

### 7. Checking Your Work Against the Reference

If you get stuck, `../solution/` has a complete, working implementation with the same class and method names EXERCISE.md asks you to build. `../tests/` has real Testcontainers-backed tests against that reference implementation -- reading the test names and assertions is often a faster way to understand *what a method is supposed to return* than reading the implementation itself.

To build everything (including the reference solution and tests) from the repo root:

```bash
dotnet build 13-GraphDatabaseNeo4j/GraphTraversals/GraphTraversals/GraphTraversals.csproj
dotnet build 13-GraphDatabaseNeo4j/GraphTraversals/solution/GraphTraversals.Solution.csproj
dotnet build 13-GraphDatabaseNeo4j/GraphTraversals/tests/GraphTraversals.Tests.csproj
```

The tests spin up their own Neo4j container via Testcontainers (separate from the `neo4j-traversals` container you started above), so Docker needs to be running, but you don't need `docker compose up` for the tests specifically:

```bash
dotnet test 13-GraphDatabaseNeo4j/GraphTraversals/tests/GraphTraversals.Tests.csproj
```

### 8. Common Commands

```bash
# Build the project
dotnet build

# Run the project
dotnet run

# Clean build artifacts
dotnet clean

# Restore dependencies
dotnet restore

# Stop the Neo4j container when you're done
cd 13-GraphDatabaseNeo4j && docker compose stop neo4j-traversals
```

### 9. Troubleshooting

**Build errors?**
- Make sure you're in the `GraphTraversals/GraphTraversals` directory (note: nested folder, same layout as every project in this repo)
- Run `dotnet restore`

**Can't connect to Neo4j?**
- Confirm the container is running: `docker compose ps`
- Confirm you're using port **7689** (bolt), not another project's port -- each of the five GraphDatabaseNeo4j projects has its own
- Check `appsettings.json` matches: `bolt://localhost:7689`, `neo4j` / `graphtraversals`

**Tests hang or fail to start a container?**
- Testcontainers needs a running Docker daemon. If you're in an environment without Docker (like a CI sandbox), the tests won't run, but `dotnet build` on the test project should still succeed -- that's the signal to check there instead.

**Cypher syntax errors mentioning `*1..N`?**
- Variable-length relationship bounds (the `*1..4` part of a pattern) must be integer *literals* in Cypher -- you cannot pass the bound as a `$parameter`. If you're parameterizing a hop count from C#, you'll need to build that part of the query string with interpolation instead (see the "Real gotchas" callout in EXERCISE.md Part 3).

### 10. Need Help?

If you get stuck:
1. Check the error message carefully
2. Review the exercise instructions
3. Compare against `../solution/` and `../tests/`
4. Run the query directly in Neo4j Browser (`http://localhost:7476`) to see the raw result before worrying about the C# around it
5. Ask Claude for guidance!

---

**Ready to start?** Open [EXERCISE.md](EXERCISE.md) and begin with Part 1!
