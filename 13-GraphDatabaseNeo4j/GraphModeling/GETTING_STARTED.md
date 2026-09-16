# Getting Started with GraphModeling

## Quick Start Guide

### 1. Start This Module's Neo4j Containers
```bash
cd 13-GraphDatabaseNeo4j
docker compose up -d
```
This starts **five** separate Neo4j containers, one per project in this
module (Neo4j Community Edition only supports one database per instance,
so unlike module 10's shared Postgres container, there's no "one
container, many databases" option here). GraphModeling's container is
`neo4j-modeling`, at `bolt://localhost:7687` / `http://localhost:7474`,
credentials `neo4j` / `graphmodeling` -- already what
`GraphModeling/appsettings.json` points at. Give it a few seconds to
report healthy before running anything against it.

### 2. Navigate to the Project
```bash
cd 13-GraphDatabaseNeo4j/GraphModeling/GraphModeling
```

### 3. Verify the Build (Docker Not Required for This Step)
```bash
dotnet build
```
Building never touches Docker or Neo4j -- only *running* `Program.cs` or
the tests does.

### 4. Project Structure

Your workspace should have this structure:
```
GraphModeling/
├── GraphModeling.csproj    # Project file with dependencies
├── Program.cs              # Entry point (you'll replace this)
├── appsettings.json        # Connection details (already set)
├── EXERCISE.md             # Step-by-step exercise guide
├── GETTING_STARTED.md      # This file
└── (folders you'll create)
    ├── Domain/             # Part 1: Person, Company
    └── Persistence/        # Part 1 onward: GraphWriter, GraphSchema,
                             # KnowsConvention (Part 3), GraphSeeder (Part 5)
```

### 5. Follow the Exercise

Open [EXERCISE.md](EXERCISE.md) and follow the step-by-step instructions.

The exercise is divided into 5 parts:
1. **Part 1**: Nodes and labels (`Person`, `Company`), parameterized Cypher
2. **Part 2**: Relationships as first-class data (`WORKS_AT` with `role`/`since`)
3. **Part 3**: Directionality (`FOLLOWS` vs. `KNOWS`)
4. **Part 4**: Uniqueness constraints and property indexes
5. **Part 5**: A seed dataset for the shared domain, idempotent by design

### 6. Running Your Code

```bash
# Run your own work
dotnet run --project GraphModeling

# See the reference solution run instead
dotnet run --project ../solution

# Check your work against the tests (needs Docker, but NOT docker compose --
# Testcontainers starts its own throwaway container)
dotnet test ../tests
```

### 7. How to Use the Reference Solution

`../solution/` uses the same namespaces and type names as `EXERCISE.md`,
so you can compare your file against its counterpart directly.

Attempt each part yourself first. Open the reference when you're stuck,
or once you've finished a part and want to compare approaches -- reading
it up front is the fastest way to feel productive and learn nothing.

The tests point at `solution/`'s types out of the box (via a direct
`ProjectReference` from `tests/`). To exercise **your** code instead,
change that `ProjectReference` in `tests/GraphModeling.Tests.csproj` to
`../GraphModeling/GraphModeling.csproj` -- the tests will fail to compile
until your types exist with matching names and signatures, which makes
them a usable checklist for how far you've got.

### 8. Inspecting the Graph Visually

Open the Neo4j Browser at `http://localhost:7474` (credentials `neo4j` /
`graphmodeling`) and run:
```cypher
MATCH (n) RETURN n
```
to see every node and relationship you've created so far. This is the
fastest way to sanity-check your work as you go through each part --
far faster than writing a read query in C# every time.

### 9. Start Exercising
Open [EXERCISE.md](EXERCISE.md) and begin with **Part 1: Nodes and Labels**.

---

## Tips for Success

### 1. Type the Code Yourself
Don't copy-paste. Typing the Cypher and the C# wrapping it is what builds
the muscle memory for "values are parameters, never string-interpolated
query text" -- the single most important habit this exercise teaches.

### 2. Use the Neo4j Browser Constantly
`MATCH (n) RETURN n` after every part is the closest thing this module
has to Postgres's `psql` or Cosmos's Data Explorer. Don't wait until
Part 5 to look at the graph for the first time.

### 3. Re-Run Part 5's Seed on Purpose
Idempotency is easy to claim and easy to get subtly wrong. Actually run
`dotnet run --project GraphModeling` twice in a row after Part 5 and
confirm the printed node count doesn't change -- don't just trust the
reasoning in the exercise text.

## Common Mistakes

❌ Building Cypher query text with string interpolation instead of `$parameters`
❌ Putting relationship properties inside a `MERGE` pattern (`MERGE (a)-[:KNOWS {since: $since}]->(b)`) instead of `MERGE`-ing the pattern and `SET`-ing properties separately -- this creates a new relationship every time a property value changes
❌ Querying `KNOWS` with a directed pattern (`(a)-[:KNOWS]->(b)`) instead of undirected (`(a)-[:KNOWS]-(b)`) -- you'll silently see only half of a person's KNOWS edges
❌ Returning an `IResultCursor` from an `ExecuteReadAsync`/`ExecuteWriteAsync` delegate instead of consuming it and returning a plain value -- the cursor is invalid once the transaction closes
❌ Forgetting `docker compose up -d` before running `Program.cs` interactively (the tests don't need this -- see below)

## Troubleshooting

### "Docker not running" / connection refused on `bolt://localhost:7687`
`docker compose up -d` needs a running Docker daemon. Start Docker
Desktop (or your Docker daemon) first, then re-run `docker compose up -d`
from `13-GraphDatabaseNeo4j/`. Running `Program.cs` before the container
exists (or before Docker itself is running) fails at
`driver.VerifyConnectivityAsync()` with a connection error, not a hang.

### "The container is still starting" -- connection errors right after `docker compose up -d`
Neo4j takes a few seconds to report healthy after the container starts,
even though `docker compose up -d` returns immediately. Check
```bash
docker compose ps
```
and wait for `neo4j-modeling` to show as healthy, or just retry
`dotnet run --project GraphModeling` a few seconds later.

### Connecting to the wrong port
This module runs **five** Neo4j containers side by side, each on its own
port pair. GraphModeling is `7474`/`7687` -- if you're getting
authentication or connection errors and you've been working across
multiple projects in this module, double-check
`GraphModeling/appsettings.json` still points at `bolt://localhost:7687`
and not one of the other four projects' ports (7688-7691). See the table
in this module's root [README.md](../README.md#running-neo4j-for-this-module)
for the full mapping.

### `Neo.ClientError.Security.Unauthorized` / authentication failure
Each of the five containers has its **own** password
(`graphmodeling`, `graphquerying`, ...). If you copy `appsettings.json`
between projects or point at the wrong port, the username (`neo4j`) will
be right but the password will be wrong for that container. Confirm
`appsettings.json`'s `Neo4j:Password` matches the container you're
actually connecting to.

### `dotnet test` hangs or fails immediately
Testcontainers needs Docker running to start its own throwaway Neo4j
container, even though you never ran `docker compose up` for the tests.
If Docker Desktop isn't running, start it first. The first test in a run
pays a real container-startup cost (image pull if not cached, then a few
seconds to become ready) -- this is expected, not a hang.

### `Neo4jBuilder()` is obsolete / won't compile
Use `new Neo4jBuilder("neo4j:5-community")` -- the parameterless
constructor is obsolete in `Testcontainers.Neo4j` 4.15.0. This matches
the image `docker-compose.yml` uses for the interactive containers, so
tests and manual runs behave the same way.

### `record["name"].As<string>()` doesn't compile / is ambiguous
This happens when both `FluentAssertions` and `Neo4j.Driver` are `using`
in the same file -- both define an `As<T>()` extension method. Call the
static method explicitly instead: `Neo4j.Driver.ValueExtensions.As<string>(record["name"])`.

### "appsettings.json not found" at runtime
Check the `.csproj` has:
```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```
(Already present in this project's `.csproj` -- only relevant if you
create a new project from scratch.)

## Key Concepts to Internalize

### 1. A Relationship Is Data, Not Just a Pointer
`WORKS_AT`'s `role` and `since` live on the edge itself, queryable and
traversable independently of either endpoint. That's the one idea this
whole project is really about -- everything else (constraints, seeding,
directionality) is scaffolding around making that idea concrete.

### 2. Directionality Is a Decision You Document, Not a Database Default
Cypher relationships are always directed. Whether that direction is
*meaningful* (`FOLLOWS`) or a *convention you imposed* (`KNOWS`) is
something only your domain knowledge can answer -- the database will
never tell you which case you're in.

### 3. `MERGE` Is What Makes Idempotency Free
Every idempotency guarantee in Part 5 traces back to `GraphWriter`'s
methods using `MERGE`, written back in Parts 1-3. There's no separate
"idempotent seeding" technique to learn -- it falls out of writes you'd
want to be safe to retry anyway.

## After Completing This Project

You'll understand:
- How to create nodes and relationships safely, through parameterized
  Cypher
- Why a graph relationship can do things a relational foreign key or an
  embedded document can't
- How to choose and document a directionality convention for a
  symmetric relationship
- What a uniqueness constraint prevents that `MERGE` alone doesn't
- How to write and verify an idempotent seed method

## Next Steps

1. Complete Parts 1 through 5
2. Compare against `../solution/`
3. Run `dotnet test ../tests` and confirm everything passes (needs Docker)
4. Move to **GraphQuerying**: Cypher fundamentals in depth against this
   same domain, seeded independently in its own container

---

**Ready to start?** Open [EXERCISE.md](EXERCISE.md) and begin with Part 1!
