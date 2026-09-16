# Getting Started with GraphAlgorithms

## Quick Start Guide

### 1. Start the Neo4j + GDS Container

From the `13-GraphDatabaseNeo4j` folder (one level up from this project):

```bash
docker compose up -d neo4j-algorithms
```

**Important:** this container is slower to become ready than the module's other four Neo4j containers. It has `NEO4J_PLUGINS: '["graph-data-science"]'` set, so on first start it downloads and installs the GDS plugin before Neo4j finishes booting. Give it a minute or two -- don't assume it's broken just because the driver can't connect right away.

Watch it come up if you want to see the plugin load:
```bash
docker logs -f netlearn-graphalgorithms-neo4j
```

### 2. Verify GDS Actually Loaded

Before writing any code, confirm the plugin is there. Either:

**Option A -- Neo4j Browser:**
Open `http://localhost:7477` in your browser, log in with `neo4j` / `graphalgorithms`, and run:
```cypher
RETURN gds.version();
```

**Option B -- driver, from a scratch script or the workspace's `Program.cs`:**
```csharp
await using var session = driver.AsyncSession();
var version = await session.ExecuteReadAsync(async tx =>
{
    var cursor = await tx.RunAsync("RETURN gds.version() AS version");
    var record = await cursor.SingleAsync();
    return record["version"].As<string>();
});
Console.WriteLine($"GDS version: {version}");
```

If either of these returns a version string (rather than an error about an unknown function), you're ready. If it errors, the plugin likely hasn't finished loading -- wait a bit longer and try again.

### 3. Navigate to the Project

```bash
cd 13-GraphDatabaseNeo4j/GraphAlgorithms/GraphAlgorithms
```

### 4. Verify the Project Setup

```bash
dotnet build
```

You should see a successful build message.

### 5. Project Structure

Your workspace starts minimal:
```
GraphAlgorithms/
├── GraphAlgorithms.csproj       # Project file with dependencies
├── Program.cs                   # Entry point (you'll build this out)
├── appsettings.json              # Already points at bolt://localhost:7690
└── (folders you'll create, e.g. Seeding/)
```

Connection settings are already in `appsettings.json`:
```json
{
  "Neo4j": {
    "Uri": "bolt://localhost:7690",
    "Username": "neo4j",
    "Password": "graphalgorithms"
  }
}
```
Note the port: **7690**, not the default 7687 -- each of the five projects in this module runs its own container on its own port pair (see `../docker-compose.yml`).

### 6. Follow the Exercise

Open [EXERCISE.md](EXERCISE.md) and follow the step-by-step instructions:
1. **Part 1**: Understand why GDS projects before computing
2. **Part 2**: Seed the graph, project it, understand the catalog lifecycle
3. **Part 3**: PageRank
4. **Part 4**: Louvain community detection
5. **Part 5**: Node Similarity

### 7. Running Your Code

```bash
dotnet run
```

### 8. Compare Against the Reference Solution

If you get stuck, `../solution/` has a complete, working implementation:
```bash
cd ../solution
dotnet run
```

### 9. Common Commands

```bash
# Build the project
dotnet build

# Run the project
dotnet run

# Clean build artifacts
dotnet clean

# Restore dependencies
dotnet restore

# Run only the fast unit tests (no Docker needed)
cd ../tests && dotnet test --filter Category=Unit

# Run everything, including the Docker-backed integration tests
cd ../tests && dotnet test
```

### 10. Troubleshooting

**Can't connect to Neo4j / "Unable to connect to database"?**
- Confirm the container is actually up: `docker ps | grep graphalgorithms`
- Confirm you're using port **7690** (bolt), not 7687
- Give the container more time -- GDS plugin loading is slow on first start

**`gds.version()` or `gds.graph.project` says "unknown function/procedure"?**
- The GDS plugin hasn't finished loading. Check `docker logs netlearn-graphalgorithms-neo4j` for plugin install progress.
- Double check `docker-compose.yml` has `NEO4J_PLUGINS: '["graph-data-science"]'` under `neo4j-algorithms` -- only this project's container needs it.

**"A graph with name '...' already exists"?**
- You projected a graph and didn't drop it before projecting again. Run `CALL gds.graph.drop('social-network', false)` (the `false` means "don't error if it's already gone") and try again. See EXERCISE.md Part 2.3 for why this happens.

**Build errors?**
- Make sure you're in the `GraphAlgorithms/GraphAlgorithms` directory (note: nested folder)
- Run `dotnet restore`

**Need Help?**
If you get stuck:
1. Check the error message carefully
2. Review the exercise instructions
3. Compare against `../solution/`
4. Ask Claude for guidance!

---

**Ready to start?** Open [EXERCISE.md](EXERCISE.md) and begin with Part 1!
