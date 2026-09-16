# Getting Started with GraphQuerying

## Quick Start

### 1. Start This Project's Neo4j Container

This module runs five separate Neo4j containers, one per project (Community
Edition only supports one database per instance). GraphQuerying's is
`neo4j-querying`, exposed on `localhost:7475` (browser) and `localhost:7688`
(bolt), with credentials `neo4j`/`graphquerying`:

```bash
cd 13-GraphDatabaseNeo4j
docker compose up -d neo4j-querying
```

The first start takes a little while as the image downloads and Neo4j
finishes initializing. You can check the container's health with:
```bash
docker compose ps neo4j-querying
```

### 2. Navigate to This Project
```bash
cd GraphQuerying
```

### 3. Verify Setup
```bash
dotnet build GraphQuerying
```

### 4. What Is Already Here

```
GraphQuerying/
├── README.md                # The concepts behind each part
├── EXERCISE.md              # The work, in 6 parts (0 through 5)
├── GETTING_STARTED.md       # This file
│
├── GraphQuerying/           # <- YOUR WORKSPACE. Write your code here.
│   ├── GraphQuerying.csproj   #   ready to build
│   ├── Program.cs             #   replace as you work through Part 0
│   └── appsettings.json       #   already points at bolt://localhost:7688
│
├── solution/                # <- REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Domain/                 #   Person, Company
│   ├── Seed/                   #   GraphSeeder
│   ├── Queries/                #   one demo class per exercise part
│   └── (same shape as your workspace, fully implemented)
│
└── tests/                   # 23 tests, against a real Testcontainers Neo4j
```

Unlike some of this module's siblings, this project doesn't pre-create empty
`Domain/`/`Seed/`/`Queries/` folders in your workspace -- `EXERCISE.md` has
you create each file (and its folder) as you reach that part, the same way
`01-DependencyInjection/BasicDI` does.

### 5. Running Your Code

Your workspace project talks to the container directly -- no orchestration
layer needed:
```bash
dotnet run --project GraphQuerying
```

To see the reference solution run instead:
```bash
dotnet run --project solution
```

Both point at the same container and the same seed data (seeding is
idempotent, so running either -- or both, repeatedly -- is safe).

### 6. Checking Your Work

```bash
dotnet test tests
```

**This needs Docker.** The test suite doesn't reuse the container you
started in step 1 -- `tests/Neo4jFixture.cs` starts its *own* Neo4j
container via Testcontainers, once for the whole test run (an
`ICollectionFixture<Neo4jFixture>` shared across every test class), seeds it
with the same `GraphSeeder` your workspace/solution use, and tears it down
afterward. Expect the first run to take a minute or two while the container
image is pulled and Neo4j finishes starting.

The tests reference `solution/GraphQuerying.Solution.csproj` directly, so
they check the reference implementation, not your workspace code. To check
your own code instead, point `tests/GraphQuerying.Tests.csproj`'s
`ProjectReference` at `../GraphQuerying/GraphQuerying.csproj`, and make sure
your workspace exposes the same `GraphQuerying.Seed.GraphSeeder` type the
tests import.

### 7. How to Use the Reference Solution

`solution/` uses the same namespaces and type names as `EXERCISE.md`, so you
can compare your file against its counterpart directly.

Attempt each part yourself first. Open the reference when you're stuck, or
after finishing a part and wanting to compare approaches -- reading it
up front is the fastest way to feel productive and learn nothing.

### 8. Start Exercising
Open [EXERCISE.md](EXERCISE.md) and begin with **Part 0: Domain Model, Seed
Data, and Connecting** -- everything after it depends on having a graph to
query.

## Common Mistakes

### "My unsafe query in Part 2 didn't leak anything"
Double-check the quoting: the demo interpolates the malicious value inside
double quotes (`\"{maliciousInput}\"`) to build a Cypher string literal.
If your string uses single quotes instead, make sure the malicious value's
embedded `"` still breaks out of *your* quoting style -- the point is that
the value's content controls where the string literal ends, whichever
quote character you chose.

### "`dotnet test` hangs"
The Neo4j container needs Docker running, and the first pull/boot is slow --
give it a couple of minutes before assuming it's stuck. If it never
resolves, confirm Docker itself is running (`docker ps`).

### "My variable-length query fails with a syntax error near `$maxHops`"
This is expected if you try to parameterize the hop bound directly
(`*1..$maxHops`) -- Cypher doesn't allow it. Interpolate the literal
integer into the query text instead (see Part 4), which is safe here
because the value comes from trusted C# code, not external input.

### "Pagination returns a different page than I expected"
Check that your `ORDER BY` includes a tiebreaker (`p.name, p.id`, not just
`p.name`). With 50 randomly generated names, duplicates are likely, and
`SKIP`/`LIMIT` over a non-unique sort order isn't guaranteed stable across
runs.

## Troubleshooting

### "Failed to establish connection" / timeouts talking to `localhost:7688`
The container either isn't running yet or hasn't finished booting. Run
`docker compose ps neo4j-querying` from `13-GraphDatabaseNeo4j/` and check
its health status; give it another minute if it just started.

### "The client is unauthorized due to authentication failure"
Check `appsettings.json` still has `neo4j`/`graphquerying` -- if you
previously ran a `docker compose down -v` and changed the compose file's
`NEO4J_AUTH`, the container's credentials may no longer match.

### `dotnet build` fails with a nullable warning treated as an error
This repo's `Directory.Build.props` treats nullable-reference warnings as
errors on purpose. Fix the nullability issue (usually a missing null check
or an unnecessary `!`) rather than suppressing it.

## After Completing This Project

You'll understand:
- How to connect to and query Neo4j from .NET via the official driver
- Why parameterized Cypher is non-negotiable, and what specifically makes
  string-built Cypher exploitable
- How to choose between inline pattern properties and `WHERE` clauses
- How variable-length relationships answer multi-hop questions that would
  otherwise need a recursive CTE or an application-level loop
- How to aggregate and paginate Cypher results correctly, including the
  tiebreaker gotcha

## Next Steps

1. Complete Parts 0 through 5
2. Compare against `solution/`
3. Run `dotnet test tests` and make sure everything passes
4. Move on to this module's other projects -- `GraphModeling`,
   `GraphTraversals`, `GraphAlgorithms`, and `GraphTransactions`

---

**Ready to start querying a graph?** Open [EXERCISE.md](EXERCISE.md) and
begin with Part 0!
