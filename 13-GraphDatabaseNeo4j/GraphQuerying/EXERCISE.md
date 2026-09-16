# Exercise: Querying a Graph with the Neo4j .NET Driver

## Overview
In this exercise, you'll query a small professional-network graph -- people,
companies, and the relationships between them -- through the Neo4j .NET
driver. You'll write parameterized Cypher (and see exactly why the
alternative is dangerous), match increasingly specific patterns, use
variable-length relationships to answer "who's within N hops" in one query,
and finish with aggregation and pagination.

Everything here runs against **real Neo4j** in a Testcontainers-managed
container -- no mocks, no in-memory graph.

## Learning Goals
By completing this exercise, you will:
- Use `IDriver`/`IAsyncSession` and know when to call `ExecuteReadAsync` vs.
  `ExecuteWriteAsync`
- Always parameterize Cypher, and understand exactly why string-building a
  query is dangerous
- Filter on node properties and relationship properties, match multiple
  relationship types in one pattern, and choose between inline
  (`{name: $name}`) and `WHERE`-clause filtering
- Use variable-length relationships (`-[:FOLLOWS*1..3]->`) to answer
  multi-hop reachability questions in a single query
- Aggregate with `count()`/`collect()`/grouping, and page results with
  `WITH ... ORDER BY ... SKIP ... LIMIT`

---

## The Scenario

You're querying a small professional network: `Person` nodes (`id`, `name`),
`Company` nodes (`id`, `name`), and three kinds of relationships:
- `KNOWS` (undirected, has a `since` year) -- two people who know each other
- `WORKS_AT` (`Person` -> `Company`, has `role` and `since`)
- `FOLLOWS` (`Person` -> `Person`, directed, no properties) -- a one-way
  social connection, not necessarily reciprocated

The seed data is 50 people, 6 companies, and a dense mesh of relationships --
dense enough that filtering, aggregation, and pagination queries return
meaningfully different result sets depending on the parameters you pass.

---

## Part 0: Domain Model, Seed Data, and Connecting

Before you can query anything, you need a driver connection and data to
query.

### Step 0.1: Create the Domain Types

**Your Task:**
Create `Domain/Person.cs` and `Domain/Company.cs`:

```csharp
// Domain/Person.cs
namespace GraphQuerying.Domain;

public record Person(string Id, string Name);
```

```csharp
// Domain/Company.cs
namespace GraphQuerying.Domain;

public record Company(string Id, string Name);
```

These aren't mapped to nodes by any ORM-like layer -- Cypher results come
back as `IRecord`s with named fields, and you'll read them into these types
(or plain values) yourself. There's no hidden change-tracking the way EF
Core's `DbContext` has; every write is an explicit Cypher statement.

### Step 0.2: Write an Idempotent Seeder

**Your Task:**
Create `Seed/GraphSeeder.cs`. Generate ~50 `Person` nodes, 6 `Company`
nodes, and relationship edges for `WORKS_AT`, `KNOWS`, and `FOLLOWS` from a
**fixed random seed** (so the data is the same every run), and write them
with `MERGE` so re-running the seeder against an already-seeded database is
a no-op rather than a duplicate-data bug:

```csharp
public static async Task SeedAsync(IDriver driver)
{
    await using var session = driver.AsyncSession();

    await session.ExecuteWriteAsync(async tx =>
    {
        await (await tx.RunAsync(
                "CREATE CONSTRAINT person_id_unique IF NOT EXISTS FOR (p:Person) REQUIRE p.id IS UNIQUE"))
            .ConsumeAsync();
        await (await tx.RunAsync(
                "CREATE CONSTRAINT company_id_unique IF NOT EXISTS FOR (c:Company) REQUIRE c.id IS UNIQUE"))
            .ConsumeAsync();
    });

    await session.ExecuteWriteAsync(async tx =>
    {
        await (await tx.RunAsync(
                "UNWIND $rows AS row MERGE (p:Person {id: row.id}) SET p.name = row.name",
                new { rows = /* List<Dictionary<string, object>> of your generated people */ new List<Dictionary<string, object>>() }))
            .ConsumeAsync();
    });

    // ...then the same UNWIND + MERGE shape for companies, WORKS_AT, KNOWS,
    // and FOLLOWS. See the full version in solution/Seed/GraphSeeder.cs.
}
```

**Why `UNWIND` instead of one query per row:** sending 50 people as 50
separate `RunAsync` calls means 50 network round trips. `UNWIND $rows AS
row` sends the whole batch in one query, and Neo4j iterates it server-side --
the same batching instinct as SQL's multi-row `INSERT` or Cosmos's bulk
executor.

**Why `MERGE` keyed on `id`, not `CREATE`:** `CREATE` always adds a new
node/relationship. `MERGE` looks for a match first and only creates on a
miss, which is what makes re-running the seeder safe.

**A parameter-shape gotcha:** the Neo4j .NET driver only reliably converts
nested collection items when they're `Dictionary<string, object>` -- an
anonymous object nested inside a `List<...>` parameter won't serialize the
way you'd expect. Build your rows as `List<Dictionary<string, object>>`,
even though the *top-level* parameter object can be an anonymous type.

**Questions to think about:**
1. Why does keying `MERGE` on a stable `id` property (rather than matching
   on `name`) matter here, given that two different people could plausibly
   share a name?
2. What would go wrong if you used `CREATE` instead of `MERGE` for the
   constraint-creation step's *data* writes, but kept `IF NOT EXISTS` only
   on the constraints themselves?

### Step 0.3: Connect and Seed from `Program.cs`

**Your Task:**
Wire up configuration and the driver, then call your seeder:

```csharp
using Microsoft.Extensions.Configuration;
using Neo4j.Driver;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var uri = configuration["Neo4j:Uri"] ?? throw new InvalidOperationException("Missing Neo4j:Uri.");
var username = configuration["Neo4j:Username"] ?? throw new InvalidOperationException("Missing Neo4j:Username.");
var password = configuration["Neo4j:Password"] ?? throw new InvalidOperationException("Missing Neo4j:Password.");

await using var driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
await driver.VerifyConnectivityAsync();

Console.WriteLine($"Connected to Neo4j at {uri}");

await GraphSeeder.SeedAsync(driver);
Console.WriteLine("Seed data ensured.");
```

`appsettings.json` already points at this project's own container
(`bolt://localhost:7688`, `neo4j`/`graphquerying`) -- see
`GETTING_STARTED.md` for starting it.

---

## Part 1: The Driver's Read/Write Split

**Your Task:**
Create `Queries/DriverBasicsDemo.cs` and run a couple of read-only queries
through `ExecuteReadAsync`:

```csharp
using Neo4j.Driver;

namespace GraphQuerying.Queries;

public static class DriverBasicsDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        var personCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:Person) RETURN count(p) AS total");
            var record = await cursor.SingleAsync();
            return record["total"].As<long>();
        });

        Console.WriteLine($"Total Person nodes: {personCount}");
    }
}
```

Call it from `Program.cs` after seeding: `await DriverBasicsDemo.RunAsync(driver);`

**Why this matters:** `ExecuteReadAsync`/`ExecuteWriteAsync` aren't just
naming conventions -- against a real Neo4j **cluster**, the driver uses them
to route reads to any available replica and writes to the current leader.
Against this exercise's single instance there's no visible difference, but
calling `ExecuteReadAsync` for something that writes will fail outright
(Neo4j rejects writes inside a read transaction), and calling
`ExecuteWriteAsync` for a pure read works but defeats read-scaling in a
cluster. Get the habit right here, even though the payoff only shows up
elsewhere.

Notice the delegate you pass to `ExecuteWriteAsync`/`ExecuteReadAsync`
**consumes the cursor before returning** (`SingleAsync`, `ToListAsync`,
`ConsumeAsync`) rather than returning the raw `IResultCursor`. Returning an
open cursor from inside the transaction function is obsolete in the current
driver -- the transaction may already be closed by the time you'd try to
read from it outside the delegate.

**Questions to think about:**
1. What actually breaks (and how) if you run a `CREATE` statement inside
   `ExecuteReadAsync` against a single Neo4j instance?
2. In a 3-node causal cluster, what could go wrong if you read
   immediately after a write, using a **different session** than the one
   that did the write?

---

## Part 2: Parameterized Queries

**Your Task:**
Create `Queries/ParameterizedQueriesDemo.cs`. First, build a query by string
interpolation and watch it go wrong; then fix it with a parameter:

```csharp
using Neo4j.Driver;

namespace GraphQuerying.Queries;

public static class ParameterizedQueriesDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        // Shaped like a Cypher-injection payload -- NOT a real person name.
        var maliciousInput = "Nobody\" OR 1=1 //";

        // NEVER do this -- string-built Cypher is a Cypher injection hole
        // exactly like string-built SQL:
        var unsafeQuery = $"MATCH (p:Person) WHERE p.name = \"{maliciousInput}\" RETURN p.name AS name";
        var unsafeCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(unsafeQuery);
            var records = await cursor.ToListAsync();
            return records.Count;
        });
        Console.WriteLine($"[UNSAFE] matched {unsafeCount} people (should be 0 -- 'OR 1=1' leaked everyone)");

        // The safe version: the SAME value, passed as a PARAMETER.
        var safeCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person) WHERE p.name = $name RETURN p.name AS name",
                new { name = maliciousInput });
            var records = await cursor.ToListAsync();
            return records.Count;
        });
        Console.WriteLine($"[SAFE] matched {safeCount} people (0 -- the payload is inert)");
    }
}
```

Run it and watch the two counts. The first leaks every `Person` node in the
database; the second correctly finds none, because nobody is actually named
`Nobody" OR 1=1 //`.

**Why this matters:** this is the exact same failure mode as module 10's
raw-SQL injection (`FromSqlRaw` + concatenation) and module 11's Cosmos SQL
injection (a hand-built `WHERE` clause), just in Cypher. `$name` sends the
value to the server as data, tagged as a parameter -- it is never parsed as
part of the query's grammar, no matter what characters it contains. String
interpolation pastes the value directly into the text the Cypher parser
reads, so a value containing `"`, `OR`, or `//` can change what the query
*means*, not just what it matches.

**Questions to think about:**
1. The unsafe query above used `OR 1=1` to match everything. Sketch (don't
   necessarily run) a payload that would instead let an attacker `DETACH
   DELETE` arbitrary nodes if this query were ever used inside a write
   transaction.
2. `tx.RunAsync(query, new { name })` accepts an anonymous object at the
   top level. Why does the earlier `UNWIND $rows AS row` seeding query need
   `Dictionary<string, object>` for the *nested* rows instead of anonymous
   objects there too?

---

## Part 3: Pattern Matching

**Your Task:**
Create `Queries/PatternMatchingDemo.cs` covering four flavors of pattern
matching:

```csharp
using Neo4j.Driver;

namespace GraphQuerying.Queries;

public static class PatternMatchingDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        // 1. Inline pattern property: the filter is part of the shape.
        var byInlineId = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person {id: $id}) RETURN p.name AS name",
                new { id = "p0" });
            var record = await cursor.SingleAsync();
            return record["name"].As<string>();
        });

        // 2. WHERE clause: needed for comparisons -- ">=" can't be
        //    expressed as an inline "{...}" property.
        var recentHires = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person)-[r:WORKS_AT]->(c:Company {id: $companyId})
                WHERE r.since >= $sinceYear
                RETURN count(p) AS total
                """,
                new { companyId = "c0", sinceYear = 2020 });
            var record = await cursor.SingleAsync();
            return record["total"].As<long>();
        });

        // 3. Filtering by a RELATIONSHIP property.
        var recentKnows = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (:Person)-[r:KNOWS]-(:Person) WHERE r.since > $year RETURN count(r) AS total",
                new { year = 2020 });
            var record = await cursor.SingleAsync();
            return record["total"].As<long>() / 2; // undirected edges are counted from both ends
        });

        // 4. Multiple relationship types in ONE pattern.
        var connections = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (:Person {id: $id})-[:KNOWS|FOLLOWS]-(other:Person) RETURN count(DISTINCT other) AS total",
                new { id = "p0" });
            var record = await cursor.SingleAsync();
            return record["total"].As<long>();
        });

        Console.WriteLine($"p0's name (inline match): {byInlineId}");
        Console.WriteLine($"People who joined c0 since 2020: {recentHires}");
        Console.WriteLine($"KNOWS relationships formed after 2020: {recentKnows}");
        Console.WriteLine($"People p0 knows or follows/is followed by: {connections}");
    }
}
```

**Inline `{...}` vs. `WHERE` -- when each reads better:**
- Inline pattern properties (`(p:Person {id: $id})`) read best for equality
  checks that pin down *which* node or relationship you're matching --
  the filter is part of the shape you're describing.
- `WHERE` is required for anything beyond equality (`>=`, `<>`, `CONTAINS`,
  boolean combinations across multiple variables) and reads better once you
  have more than one simple equality condition, because stacking several
  `{...}` blocks across a long pattern gets visually noisy fast.

**Why filtering an undirected relationship needs the `/ 2`:** you seeded
`KNOWS` with `MERGE (a)-[r:KNOWS]-(b)` (no arrow). Matching it back with
`-[r:KNOWS]-` (also no arrow) finds each edge from *both* of its endpoints,
so a naive `count(r)` double-counts. `FOLLOWS`, being genuinely directed, has
no such gotcha when matched with `->`.

**Questions to think about:**
1. Why can't `WHERE r.since >= $sinceYear` be rewritten as an inline
   pattern property?
2. If you matched `KNOWS` with `-[r:KNOWS]->` (an arrow) instead of
   `-[r:KNOWS]-`, what would change about which edges you'd find, given how
   the seeder created them?

---

## Part 4: Variable-Length Relationships

**Your Task:**
Create `Queries/VariableLengthDemo.cs`:

```csharp
using Neo4j.Driver;

namespace GraphQuerying.Queries;

public static class VariableLengthDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        foreach (var maxHops in new[] { 1, 2, 3 })
        {
            var reachable = await session.ExecuteReadAsync(async tx =>
            {
                var query =
                    $"MATCH (start:Person {{id: $id}})-[:FOLLOWS*1..{maxHops}]->(reached:Person) " +
                    "WHERE reached <> start " +
                    "RETURN count(DISTINCT reached) AS total";
                var cursor = await tx.RunAsync(query, new { id = "p0" });
                var record = await cursor.SingleAsync();
                return record["total"].As<long>();
            });

            Console.WriteLine($"Reachable from p0 within {maxHops} hop(s): {reachable}");
        }
    }
}
```

Run it and watch the count grow (usually a lot) between 1, 2, and 3 hops --
that growth *is* the point.

**Why this one Cypher construct replaces a recursive CTE or an
application-level loop:** finding "everyone within N hops" against
module 10's Postgres needs a recursive CTE (`WITH RECURSIVE`) that
self-joins on every iteration, and against module 11's Cosmos DB there's no
query-language answer at all -- you'd fetch one level, then issue another
query per node in that level, in application code, accumulating round
trips as the frontier grows. `-[:FOLLOWS*1..3]->` asks the same question as
a single Cypher statement, and the storage engine walks the relationships
directly (each relationship is a physical pointer, not a join computed at
query time) rather than computing a join across a whole table repeatedly.

**A real limitation to notice, not a contradiction of Part 2:** the hop
bound in `*1..{maxHops}` is interpolated as a literal, not passed as
`$maxHops`. Neo4j does not allow parameters inside the structural part of a
pattern -- the number of hops changes what the query *is*, not just what
value it compares against, so it can't be a parameter the way `$id` can.
This is safe here because `maxHops` comes from a fixed C# loop, never from
untrusted external text; it is not the same risk Part 2 warned about, where
an attacker-controlled *string* was spliced into the query.

**Questions to think about:**
1. Why does `WHERE reached <> start` matter for `*1..3` specifically, given
   that a 1-hop-only query (`*1..1`) could never revisit the start node?
2. If you needed the hop count to come from a user-facing "search depth"
   control, how would you keep it safe without parameterizing it directly?

---

## Part 5: Aggregation and Pagination

**Your Task:**
Create `Queries/AggregationAndPaginationDemo.cs`:

```csharp
using Neo4j.Driver;

namespace GraphQuerying.Queries;

public static class AggregationAndPaginationDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        // Grouping: count(p) resets per distinct c.name automatically --
        // Cypher groups by every non-aggregated returned expression, there's
        // no separate GROUP BY clause like SQL's.
        var perCompany = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person)-[:WORKS_AT]->(c:Company)
                RETURN c.name AS company, count(p) AS employees
                ORDER BY employees DESC
                """);
            var records = await cursor.ToListAsync();
            return records.Select(r => (r["company"].As<string>(), r["employees"].As<long>())).ToList();
        });

        foreach (var (company, employees) in perCompany)
        {
            Console.WriteLine($"{company}: {employees} employees");
        }

        // collect(): fold matching rows into one list value.
        var knownNames = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (:Person {id: $id})-[:KNOWS]-(other:Person) RETURN collect(other.name) AS names",
                new { id = "p0" });
            var record = await cursor.SingleAsync();
            return record["names"].As<List<string>>();
        });
        Console.WriteLine($"p0 knows: {string.Join(", ", knownNames)}");

        // Pagination: WITH re-pipes the ordered stream so SKIP/LIMIT apply
        // to a stable order, not an arbitrary one.
        const int pageSize = 20;
        var page0 = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person)
                WITH p
                ORDER BY p.name, p.id
                SKIP $skip LIMIT $limit
                RETURN p.name AS name
                """,
                new { skip = 0, limit = pageSize });
            var records = await cursor.ToListAsync();
            return records.Select(r => r["name"].As<string>()).ToList();
        });
        Console.WriteLine($"Page 0: {string.Join(", ", page0)}");
    }
}
```

**Why `ORDER BY p.name, p.id` and not just `p.name`:** with 50 randomly
generated people drawn from a limited name pool, two people can end up with
the same name. Without a tiebreaker, `SKIP`/`LIMIT` pagination over ties in
`p.name` isn't guaranteed stable between calls -- a name that sits right on
a page boundary could shift pages between runs. Adding the unique `p.id` as
a secondary sort key makes the order, and therefore the pages, deterministic.

**Questions to think about:**
1. Why does `count(p)` need to appear in the `RETURN` clause for grouping to
   happen at all -- what would `RETURN c.name` alone (no aggregate) return
   instead?
2. What would happen to `collect(other.name)` if you queried a person with
   zero `KNOWS` relationships -- an empty list, `null`, or an error?
3. If you needed a "total pages" count alongside a page of results, would
   you compute it from a second `count()` query or try to get it from the
   same query as the page? Why?

---

## Reflection Questions

1. **Why is `ExecuteWriteAsync`/`ExecuteReadAsync` worth using consistently**
   even against a single Neo4j instance where they behave identically?
2. **What specifically makes `$name` safe and string interpolation unsafe**,
   given that both end up sending bytes over the same Bolt connection?
3. **When would inline pattern-property matching read worse than a `WHERE`
   clause**, even for a simple equality check?
4. **Why can't Cypher's variable-length hop bound be a parameter**, and what
   does that tell you about the difference between a query's *structure* and
   its *data*?
5. **Contrast how "find everyone within 3 hops" would be solved in each of
   this repo's three data stores** -- Postgres (recursive CTE), Cosmos DB
   (application-level loop), and Neo4j (`*1..3`). What does the difference
   tell you about when a graph database is the right tool?

---

## Summary

You've learned:
- `IDriver`/`IAsyncSession`, and the read/write routing behind
  `ExecuteReadAsync`/`ExecuteWriteAsync`
- Parameterized Cypher as the only safe way to build a query from external
  input, and exactly how string-built Cypher breaks
- Node-property, relationship-property, and multi-relationship-type pattern
  matching, and when inline `{...}` matching reads better than `WHERE`
- Variable-length relationships (`-[:FOLLOWS*1..3]->`) for multi-hop
  reachability in one query
- Aggregation (`count()`, `collect()`, implicit grouping) and pagination
  with `WITH ... ORDER BY ... SKIP ... LIMIT`

## Next Steps

- Compare your work against [`solution/`](solution/)
- Run the test suite: `dotnet test tests` (needs Docker for Testcontainers)
- Explore the other projects in this module: data modeling
  (`GraphModeling`), traversal algorithms (`GraphTraversals`), graph
  algorithms (`GraphAlgorithms`), and transactions (`GraphTransactions`)
