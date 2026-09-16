# Exercise: Traversals -- Shortest Paths, Bounded Depth, and Degrees of Separation

## Overview
In this exercise, you'll build a small library of Neo4j traversal queries against a professional-network graph: people who KNOW and FOLLOW each other, and WORK_AT companies. You'll start by seeding a graph whose shape is deliberately interesting (hubs, an "island" cluster, a pair connected only through several intermediaries), then write queries that find the shortest path between two people, find *every* tied-shortest path, bound how far a traversal is allowed to search, find mutual connections, and group everyone reachable from a person by exact hop distance.

## Learning Goals
By completing this exercise, you will:
- Return an actual path from Cypher, not just a yes/no or a count, and read the intermediate nodes off of it
- Know when `allShortestPaths()` is the right tool instead of `shortestPath()`
- Bound a variable-length relationship pattern and explain why an unbounded one is risky
- Match a relationship as undirected or directed depending on what it actually means
- Write a mutual-connections query and a degrees-of-separation query
- Compare how "how are these two things connected" is answered in Neo4j versus Postgres (module 10) and Cosmos DB (module 11)

---

## The Scenario

You're extending the professional-network graph from earlier in this module:
- **`Person`** nodes (`id`, `name`)
- **`Company`** nodes (`id`, `name`)
- **`KNOWS`** relationships between people (`since`) -- conceptually symmetric: if Alice knows Bob, Bob knows Alice
- **`WORKS_AT`** relationships from a person to a company (`role`, `since`)
- **`FOLLOWS`** relationships between people (directed, and *not* symmetric -- following someone doesn't mean they follow you back)

For this exercise, the graph is seeded with a specific shape so that traversal queries have real, non-trivial answers instead of "everyone is one hop from everyone else":

- **Two hubs**: `alice` and `bob` each directly `KNOWS` five other people.
- **A pair with no direct connection**: `erin` and `ivan` don't know each other and share no mutual friend -- the only route between them is `erin -> alice -> bob -> ivan`, three hops.
- **A tied shortest path**: `carol` and `grace` are two hops apart in exactly two ways -- through `bob`, and through `judy`.
- **A loosely-attached island**: `mallory`, `niaj`, and `olivia` know each other, but the graph connects to that trio through exactly one edge, `judy-mallory`. `peggy` hangs off `olivia` alone, five hops from `alice` -- deliberately too far for a bounded `*1..3` query to reach.

---

## Part 0: Connect and Seed the Graph

### Step 0.1: Create the Seed Data

**Your Task:**
Create a file called `GraphSeeder.cs`:

```csharp
using Neo4j.Driver;

namespace GraphTraversals;

public static class GraphSeeder
{
    public static async Task SeedAsync(IDriver driver, CancellationToken cancellationToken = default)
    {
        await using var session = driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "CREATE CONSTRAINT person_id_unique IF NOT EXISTS FOR (p:Person) REQUIRE p.id IS UNIQUE");
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "CREATE CONSTRAINT company_id_unique IF NOT EXISTS FOR (c:Company) REQUIRE c.id IS UNIQUE");
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $people AS person
                MERGE (p:Person {id: person.id})
                SET p.name = person.name
                """,
                new { people = People });
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $companies AS company
                MERGE (c:Company {id: company.id})
                SET c.name = company.name
                """,
                new { companies = Companies });
            await cursor.ConsumeAsync();
        });

        // KNOWS is stored as a single directed relationship -- the "from" side
        // is just an artifact of which order the edge list happens to list the
        // pair in. Every query in this exercise matches it undirected with
        // -[:KNOWS]-, because "knowing someone" is conceptually symmetric.
        // Modeling it as one directed edge but always querying it undirected
        // is a common, valid pattern for relationships like this -- it avoids
        // ever having to create (and keep in sync) two edges for one fact.
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $edges AS edge
                MATCH (a:Person {id: edge.a}), (b:Person {id: edge.b})
                MERGE (a)-[r:KNOWS]->(b)
                SET r.since = edge.since
                """,
                new { edges = KnowsEdges });
            await cursor.ConsumeAsync();
        });

        // FOLLOWS, unlike KNOWS, is genuinely directed and asymmetric: Alice is
        // followed by Carol, Grace, and Heidi, but Alice's own follow of Bob is
        // not reciprocated.
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $edges AS edge
                MATCH (a:Person {id: edge.from}), (b:Person {id: edge.to})
                MERGE (a)-[r:FOLLOWS]->(b)
                """,
                new { edges = FollowsEdges });
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $edges AS edge
                MATCH (p:Person {id: edge.personId}), (c:Company {id: edge.companyId})
                MERGE (p)-[r:WORKS_AT]->(c)
                SET r.role = edge.role, r.since = edge.since
                """,
                new { edges = WorksAtEdges });
            await cursor.ConsumeAsync();
        });
    }

    public static readonly IReadOnlyList<object> People =
    [
        new { id = "alice", name = "Alice Nakamura" },
        new { id = "bob", name = "Bob Okafor" },
        new { id = "carol", name = "Carol Jimenez" },
        new { id = "dave", name = "Dave Whitfield" },
        new { id = "erin", name = "Erin Kowalski" },
        new { id = "frank", name = "Frank Delgado" },
        new { id = "grace", name = "Grace Lindqvist" },
        new { id = "heidi", name = "Heidi Mercer" },
        new { id = "ivan", name = "Ivan Petrov" },
        new { id = "judy", name = "Judy Alvarez" },
        new { id = "mallory", name = "Mallory Chen" },
        new { id = "niaj", name = "Niaj Rahman" },
        new { id = "olivia", name = "Olivia Ferreira" },
        new { id = "peggy", name = "Peggy Sato" },
    ];

    public static readonly IReadOnlyList<object> Companies =
    [
        new { id = "techcorp", name = "TechCorp" },
        new { id = "datasystems", name = "DataSystems" },
        new { id = "startupx", name = "StartupX" },
    ];

    public static readonly IReadOnlyList<object> KnowsEdges =
    [
        new { a = "alice", b = "bob", since = 2015 },
        new { a = "alice", b = "carol", since = 2016 },
        new { a = "alice", b = "dave", since = 2017 },
        new { a = "alice", b = "erin", since = 2018 },
        new { a = "alice", b = "frank", since = 2018 },
        new { a = "bob", b = "carol", since = 2016 },
        new { a = "bob", b = "grace", since = 2019 },
        new { a = "bob", b = "heidi", since = 2020 },
        new { a = "bob", b = "ivan", since = 2021 },
        new { a = "carol", b = "judy", since = 2019 },
        new { a = "dave", b = "judy", since = 2020 },
        new { a = "grace", b = "judy", since = 2021 },
        new { a = "erin", b = "frank", since = 2017 },
        new { a = "judy", b = "mallory", since = 2022 }, // the one bridge edge into the island
        new { a = "mallory", b = "niaj", since = 2022 },
        new { a = "mallory", b = "olivia", since = 2023 },
        new { a = "niaj", b = "olivia", since = 2023 },
        new { a = "olivia", b = "peggy", since = 2024 },
    ];

    public static readonly IReadOnlyList<object> FollowsEdges =
    [
        new { from = "alice", to = "bob" }, // one-directional: bob does not follow alice back
        new { from = "alice", to = "carol" },
        new { from = "carol", to = "alice" },
        new { from = "grace", to = "alice" },
        new { from = "heidi", to = "alice" },
        new { from = "ivan", to = "bob" },
        new { from = "judy", to = "bob" },
        new { from = "dave", to = "judy" },
        new { from = "erin", to = "frank" },
        new { from = "mallory", to = "judy" },
        new { from = "niaj", to = "mallory" },
        new { from = "olivia", to = "mallory" },
        new { from = "peggy", to = "olivia" },
    ];

    public static readonly IReadOnlyList<object> WorksAtEdges =
    [
        new { personId = "alice", companyId = "techcorp", role = "Engineering Manager", since = 2015 },
        new { personId = "bob", companyId = "techcorp", role = "Staff Engineer", since = 2016 },
        new { personId = "carol", companyId = "techcorp", role = "Product Manager", since = 2018 },
        new { personId = "dave", companyId = "datasystems", role = "Data Engineer", since = 2017 },
        new { personId = "erin", companyId = "datasystems", role = "Analyst", since = 2019 },
        new { personId = "mallory", companyId = "startupx", role = "Founder", since = 2022 },
        new { personId = "niaj", companyId = "startupx", role = "Backend Engineer", since = 2023 },
    ];
}
```

**Why:** Every write here uses `MERGE`, so calling `SeedAsync` against an already-seeded database re-applies the same values instead of creating duplicates. That matters because you'll run this program more than once against the same container while you're iterating on the query methods below.

**Questions to think about:**
1. If `KnowsEdges` only lists `alice -> bob` once, how does a query like `(alice)-[:KNOWS]-(bob)` find it, given the edge was created in one direction?
2. Why is `MERGE` the right choice here instead of `CREATE`?

### Step 0.2: Wire Up Program.cs

**Your Task:**
Update `Program.cs`:

```csharp
using GraphTraversals;
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
Console.WriteLine("Seeded the professional-network sample graph (idempotent -- safe to run again).");
```

**Run it:**
```bash
dotnet run
```

You should see both messages printed, and the graph populated -- confirm it in Neo4j Browser (`http://localhost:7476`) with:
```cypher
MATCH (n) RETURN n
```

---

## Part 1: `shortestPath()`

### Step 1.1: Create the Query Class

**Your Task:**
Create a file called `TraversalQueries.cs`:

```csharp
using Neo4j.Driver;

namespace GraphTraversals;

public sealed class TraversalQueries
{
    private readonly IDriver _driver;

    public TraversalQueries(IDriver driver)
    {
        _driver = driver;
    }

    public async Task<IReadOnlyList<string>> ShortestPathAsync(
        string fromId, string toId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $fromId}), (b:Person {id: $toId})
                MATCH p = shortestPath((a)-[:KNOWS*]-(b))
                RETURN [n IN nodes(p) | n.id] AS ids
                """,
                new { fromId, toId });

            if (!await cursor.FetchAsync())
            {
                return new List<string>();
            }

            return cursor.Current["ids"].As<List<string>>();
        });
    }
}
```

**Why:** `shortestPath((a)-[:KNOWS*]-(b))` doesn't just tell you *that* a and b are connected -- it binds `p` to the actual path, so `nodes(p)` gives you every node along the way, in order. Returning `[n IN nodes(p) | n.id]` (a Cypher list comprehension) does the id-extraction inside the database instead of shipping whole node objects back to C# and unpacking them there.

Note the `IAsyncSession.ExecuteReadAsync` transaction function consumes the cursor *inside* the delegate rather than returning it -- returning an `IResultCursor` out of the transaction function is obsolete in this driver version, and the transaction may already be closed by the time you'd try to read from it outside.

### Step 1.2: Try It

**Your Task:**
Add to `Program.cs`:

```csharp
var queries = new TraversalQueries(driver);

Console.WriteLine();
Console.WriteLine("=== Part 1: shortestPath (erin -> ivan) ===");
var shortest = await queries.ShortestPathAsync("erin", "ivan");
Console.WriteLine(string.Join(" -> ", shortest));
```

**Run it:**
```bash
dotnet run
```

You should see `erin -> alice -> bob -> ivan`. Erin and Ivan aren't connected directly, and don't share a mutual friend -- the shortest way from one to the other runs straight through both hubs.

**Questions to think about:**
1. What would `shortestPath()` have returned if you'd asked for a path between two people who share a direct `KNOWS` edge?
2. `shortestPath()` picks *one* shortest path if several exist at the same length. Is that a problem here? (Hint: check whether `erin` and `ivan` actually have more than one length-3 path between them, given their limited neighbors.)

---

## Part 2: `allShortestPaths()`

### Step 2.1: When One Path Isn't Enough

`carol` and `grace` are two `KNOWS` hops apart -- but not in just one way. Carol knows Bob, and Bob knows Grace. Carol also knows Judy, and Judy knows Grace. Both routes are exactly two hops long, and neither is "more correct" than the other. If you only needed "are they connected, and how far," `shortestPath()` would be enough. But "show me every shortest introduction path" -- every equally-good way to get from Carol to Grace -- is a different, and common, question: think of a professional network's "how you're connected" feature, which usually shows more than one path when more than one exists.

### Step 2.2: Add the Method

**Your Task:**
Add to `TraversalQueries.cs`:

```csharp
public async Task<IReadOnlyList<IReadOnlyList<string>>> AllShortestPathsAsync(
    string fromId, string toId, CancellationToken cancellationToken = default)
{
    await using var session = _driver.AsyncSession();

    return await session.ExecuteReadAsync(async tx =>
    {
        var cursor = await tx.RunAsync(
            """
            MATCH (a:Person {id: $fromId}), (b:Person {id: $toId})
            MATCH p = allShortestPaths((a)-[:KNOWS*]-(b))
            RETURN [n IN nodes(p) | n.id] AS ids
            """,
            new { fromId, toId });

        var paths = new List<IReadOnlyList<string>>();
        while (await cursor.FetchAsync())
        {
            paths.Add(cursor.Current["ids"].As<List<string>>());
        }

        return paths;
    });
}
```

**Why:** `allShortestPaths()` returns one row per tied-shortest path, so this method loops over the cursor with `FetchAsync()` instead of taking a single record. `shortestPath()` and `allShortestPaths()` cost about the same to run -- both stop expanding as soon as they've found paths at the minimum length -- so there's rarely a reason to reach for the singular form "to save time" if what you actually want is completeness.

### Step 2.3: Try It

**Your Task:**
Add to `Program.cs`:

```csharp
Console.WriteLine();
Console.WriteLine("=== Part 2: allShortestPaths (carol <-> grace) ===");
var allShortest = await queries.AllShortestPathsAsync("carol", "grace");
foreach (var path in allShortest)
{
    Console.WriteLine(string.Join(" -> ", path));
}
```

**Run it.** You should see two lines: `carol -> bob -> grace` and `carol -> judy -> grace`.

**Questions to think about:**
1. If Carol and Grace had a *direct* `KNOWS` edge, what would `allShortestPaths()` return -- both the length-1 and the length-2 paths, or only the length-1 one?
2. Can you think of a real feature (not necessarily a social network) where "give me every shortest path" is the right question, and "give me any one shortest path" would produce a misleading answer?

---

## Part 3: Controlling Traversal Depth and Direction

### Step 3.1: Why Bound the Pattern

`-[:KNOWS*]-` with no upper bound tells Neo4j "expand this relationship as many times as it takes." On the 14-person graph in this exercise that's harmless. On a graph with a few million people, an unbounded variable-length pattern can force the query planner to explore an enormous number of paths before it's done -- especially once you add a `WHERE` clause or return something other than a shortest path, where the shortest-path early termination doesn't apply. Bounding the pattern to `*1..N` caps how far the search is allowed to go, which caps the cost.

### Step 3.2: Add a Bounded, Undirected Traversal

**Your Task:**
Add to `TraversalQueries.cs`:

```csharp
private const int MaxAllowedHops = 15;

public async Task<IReadOnlyList<string>> WithinHopsAsync(
    string startId, int maxHops, CancellationToken cancellationToken = default)
{
    ValidateHops(maxHops);

    await using var session = _driver.AsyncSession();

    return await session.ExecuteReadAsync(async tx =>
    {
        // Raw string literal interpolation ($"""...""") has brace-counting
        // rules that fight with Cypher's own {property: $param} syntax, so
        // this query is built as a regular verbatim interpolated string
        // instead: {{ and }} are literal braces, {maxHops} interpolates.
        var cursor = await tx.RunAsync(
            $@"
            MATCH (start:Person {{id: $startId}})-[:KNOWS*1..{maxHops}]-(other:Person)
            RETURN DISTINCT other.id AS id
            ",
            new { startId });

        var ids = new List<string>();
        while (await cursor.FetchAsync())
        {
            ids.Add(cursor.Current["id"].As<string>());
        }

        return ids;
    });
}

private static void ValidateHops(int maxHops)
{
    if (maxHops is < 1 or > MaxAllowedHops)
    {
        throw new ArgumentOutOfRangeException(
            nameof(maxHops), maxHops, $"maxHops must be between 1 and {MaxAllowedHops}.");
    }
}
```

**Real gotcha:** the `*1..N` part of a variable-length pattern must be an integer *literal* in Cypher -- unlike every other value in these queries, it cannot be passed as a `$parameter`. That's why `maxHops` is interpolated directly into the query text here instead of going through the parameters object like `startId` does. This is safe because `maxHops` is a validated `int`, never raw text -- string-building a `WHERE` clause from user-supplied text would be a real injection risk, but interpolating a bounds-checked integer into a fixed position isn't.

### Step 3.3: Undirected KNOWS, Directed FOLLOWS

Add two more methods that look almost identical, except for one character:

**Your Task:**
Add to `TraversalQueries.cs`:

```csharp
public async Task<IReadOnlyList<string>> FollowersAsync(
    string personId, CancellationToken cancellationToken = default)
{
    await using var session = _driver.AsyncSession();

    return await session.ExecuteReadAsync(async tx =>
    {
        var cursor = await tx.RunAsync(
            """
            MATCH (follower:Person)-[:FOLLOWS]->(p:Person {id: $personId})
            RETURN follower.id AS id
            ORDER BY id
            """,
            new { personId });

        var ids = new List<string>();
        while (await cursor.FetchAsync())
        {
            ids.Add(cursor.Current["id"].As<string>());
        }

        return ids;
    });
}
```

**Why:** Every KNOWS query in this exercise uses `-[:KNOWS]-` with no arrow, even though `GraphSeeder` created each KNOWS edge in one specific direction (`a -> b`). That's deliberate: KNOWS is conceptually symmetric, so the *query* treats it as undirected regardless of which direction it happened to be created in. FOLLOWS is the opposite case -- it's genuinely asymmetric (Alice follows Bob, but Bob doesn't follow Alice back), so `FollowersAsync` uses `-[:FOLLOWS]->` with an explicit arrow, and getting the arrow direction backward would silently swap "who follows this person" for "who this person follows."

### Step 3.4: Try It

**Your Task:**
Add to `Program.cs`:

```csharp
Console.WriteLine();
Console.WriteLine("=== Part 3: within 3 KNOWS hops of alice ===");
var withinHops = await queries.WithinHopsAsync("alice", maxHops: 3);
Console.WriteLine(string.Join(", ", withinHops.OrderBy(id => id)));
Console.WriteLine("(peggy is 5 hops from alice and should NOT appear above)");
```

**Run it.** `mallory` should appear (she's exactly 3 hops from Alice, across the one bridge edge), but `niaj`, `olivia`, and `peggy` should not (4, 4, and 5 hops away respectively).

**Questions to think about:**
1. What would change in the result of `WithinHopsAsync("alice", 3)` if `judy-mallory` didn't exist at all?
2. Why does bounding `*1..N` help query cost even when you're not asking for a shortest path -- i.e., even when the "stop as soon as you've found the answer" trick that `shortestPath()` gets doesn't apply?

---

## Part 4: Mutual Connections

### Step 4.1: Friends in Common

"Who do Bob and Judy both know" is a single Cypher pattern: walk out one `KNOWS` hop from each of them and look for a node that's on both paths.

**Your Task:**
Add to `TraversalQueries.cs`:

```csharp
public async Task<IReadOnlyList<string>> MutualConnectionsAsync(
    string aId, string bId, CancellationToken cancellationToken = default)
{
    await using var session = _driver.AsyncSession();

    return await session.ExecuteReadAsync(async tx =>
    {
        var cursor = await tx.RunAsync(
            """
            MATCH (a:Person {id: $aId})-[:KNOWS]-(mutual:Person)-[:KNOWS]-(b:Person {id: $bId})
            WHERE mutual.id <> $aId AND mutual.id <> $bId
            RETURN DISTINCT mutual.id AS id
            ORDER BY id
            """,
            new { aId, bId });

        var ids = new List<string>();
        while (await cursor.FetchAsync())
        {
            ids.Add(cursor.Current["id"].As<string>());
        }

        return ids;
    });
}
```

**Why:** `(a)-[:KNOWS]-(mutual)-[:KNOWS]-(b)` reads almost like the English sentence: "someone that both a and b know." The `WHERE` clause guards against a degenerate case -- if `a` and `b` are themselves directly connected in a way that could otherwise let one of them match as its own "mutual" connection -- and `DISTINCT` matters because the same mutual friend could otherwise be returned once per distinct path shape into the match.

### Step 4.2: Try It

**Your Task:**
Add to `Program.cs`:

```csharp
Console.WriteLine();
Console.WriteLine("=== Part 4: mutual KNOWS connections (bob <-> judy) ===");
var mutual = await queries.MutualConnectionsAsync("bob", "judy");
Console.WriteLine(string.Join(", ", mutual));
```

**Run it.** You should see `carol, grace` -- both Bob and Judy know Carol, and both know Grace.

**Questions to think about:**
1. Bob and Judy don't know each other directly. Does `MutualConnectionsAsync` care whether `a` and `b` are directly connected? Should it?
2. Try `MutualConnectionsAsync("erin", "grace")` on paper first: Erin only knows Alice and Frank; Grace only knows Bob and Judy. What should come back, and why?

---

## Part 5: Degrees of Separation

### Step 5.1: Group by Exact Hop Distance

"Who's reachable from Alice, and how far away is each person" is a different question from "who's reachable within N hops" (Part 3 answers that one as a flat set) -- this one wants the *exact* minimum distance to each person, grouped.

**Your Task:**
Add to `TraversalQueries.cs`:

```csharp
public async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> DegreesOfSeparationAsync(
    string startId, int maxHops, CancellationToken cancellationToken = default)
{
    ValidateHops(maxHops);

    await using var session = _driver.AsyncSession();

    var byHop = await session.ExecuteReadAsync(async tx =>
    {
        var cursor = await tx.RunAsync(
            $@"
            MATCH p = (start:Person {{id: $startId}})-[:KNOWS*1..{maxHops}]-(other:Person)
            WITH other, min(length(p)) AS hops
            RETURN other.id AS id, hops
            ORDER BY hops, id
            ",
            new { startId });

        var results = new List<(string Id, int Hops)>();
        while (await cursor.FetchAsync())
        {
            results.Add((cursor.Current["id"].As<string>(), cursor.Current["hops"].As<int>()));
        }

        return results;
    });

    return byHop
        .GroupBy(r => r.Hops)
        .ToDictionary(
            g => g.Key,
            IReadOnlyList<string> (g) => g.Select(r => r.Id).ToList());
}
```

**Why:** The same `(other)` node can be reached by more than one path of more than one length within the bound -- Cypher's `*1..N` pattern returns one row per matching *path*, not one row per node. `WITH other, min(length(p)) AS hops` collapses that down to each node's single minimum distance before grouping in C#, so someone reachable in both 2 and 4 hops is reported once, at 2.

### Step 5.2: Try It

**Your Task:**
Add to `Program.cs`:

```csharp
Console.WriteLine();
Console.WriteLine("=== Part 5: degrees of separation from alice (up to 5 hops) ===");
var degrees = await queries.DegreesOfSeparationAsync("alice", maxHops: 5);
foreach (var (hops, ids) in degrees.OrderBy(kv => kv.Key))
{
    Console.WriteLine($"{hops} hop(s): {string.Join(", ", ids)}");
}
```

**Run it.** You should see five groups: Alice's five direct connections at 1 hop, four more people at 2 hops, `mallory` alone at 3 hops (the bridge), `niaj`/`olivia` at 4 hops, and `peggy` alone at 5 hops.

### Step 5.3: The Same Question in Postgres and Cosmos DB

You don't need to implement either of these -- just work through what they'd look like.

**In Postgres (module 10)**, with people and a `knows` join table, you'd write a **recursive CTE**: start with the base case (`hops = 1`, direct connections), then recursively join the previous hop's results back against `knows` to find the next hop, tracking which ids you've already visited so you don't loop forever around a cycle. Each additional hop is another recursive step, and the query gets more expensive (and the SQL harder to read) as the hop count grows -- a 5-hop query does meaningfully more work, and looks meaningfully scarier, than a 2-hop one.

**In Cosmos DB (module 11)**, there's no join at all, recursive or otherwise, inside a single query -- a document database has no built-in notion of "follow this reference to another document, then follow another reference from there." Answering "who's within 5 hops of Alice" would mean either running one round-trip per hop from the application (fetch Alice's connections, then fetch each of *their* connections, and so on -- five round trips, with the fetched set growing every time), or denormalizing the entire reachable set into Alice's own document ahead of time and keeping it updated as the graph changes. Neither is close to "the database answers this in one query," which is exactly what `DegreesOfSeparationAsync` does here.

**Neo4j**, by contrast, doesn't get proportionally more complex or expensive per additional hop the way the recursive CTE does -- the *pattern* (`*1..N`) is the same regardless of how large `N` is, and the traversal engine is built around exactly this kind of hop-by-hop expansion.

**Questions to think about:**
1. Try writing (on paper, no need to run it) the recursive CTE you'd need for `DegreesOfSeparationAsync("alice", 2)` in Postgres. How does it compare in length and readability to the Cypher version above?
2. If you were forced to answer "degrees of separation up to 5 hops" in Cosmos DB for a graph that changes constantly, which downside would bite you first: the cost of five round trips per query, or the cost of keeping a denormalized reachable-set field up to date on every write?

---

## Reflection Questions

After completing this exercise, answer these:

1. **What's the difference between `shortestPath()` and `allShortestPaths()`, and when would returning only one path give a misleading answer?**

2. **Why does a Cypher variable-length pattern's hop bound have to be a literal, not a parameter -- and how does that change how you'd build a query whose hop count comes from user input?**

3. **`KNOWS` is queried undirected and `FOLLOWS` is queried directed, even though both are stored as directed relationships under the hood. What's the rule for deciding which way to query a given relationship type?**

4. **How does query cost scale with hop count in Neo4j, compared to a recursive CTE in Postgres? Why is that difference structural rather than just "Neo4j happens to be faster"?**

5. **`MutualConnectionsAsync` and `WithinHopsAsync` are both two-line Cypher patterns. Why does one need a bounded `*1..N` and the other doesn't?**

---

## Summary

You've learned:
- ✅ Returning an actual path from Cypher and reading its intermediate nodes
- ✅ The difference between one shortest path and every tied-shortest path
- ✅ Bounding a variable-length pattern, and why that bound matters on a large graph
- ✅ Matching a relationship undirected or directed based on what it means, not how it was created
- ✅ Writing a mutual-connections query
- ✅ Grouping reachable nodes by exact hop distance, and why that's a different question from "who's within N hops"
- ✅ Why "how are these connected, and by how much" gets structurally harder in Postgres and impractical in Cosmos DB as the hop count grows

## Next Steps

When you're ready, move on to:
- **GraphAlgorithms** -- PageRank, community detection, and the Graph Data Science library
- **GraphTransactions** -- transactional writes and consistency in Neo4j

---

**Happy Learning!**
