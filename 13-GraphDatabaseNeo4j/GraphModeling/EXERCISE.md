# Exercise: Modeling a Professional Network in Neo4j

## Overview
In this exercise you'll model a small professional network -- people,
companies, and the relationships between them -- as a property graph in
Neo4j, and write every read and write through the official `Neo4j.Driver`
for .NET. You'll work through five parts: nodes and labels, relationships
as first-class data, directionality, uniqueness constraints and indexes,
and finally a small idempotent seed dataset. This exact domain (`Person`,
`Company`, `KNOWS`, `WORKS_AT`, `FOLLOWS`) is reused, with the same
property names, by every other project in this module -- get the shape
right here and GraphQuerying, GraphTraversals, GraphAlgorithms, and
GraphTransactions all build on it conceptually (each seeds its own,
separate Neo4j container -- nothing is shared at the database level).

You'll need a running Neo4j instance for this exercise -- unlike some
other modules, there's no way to inspect a graph model without actually
writing to a database. From this module's root:

```bash
cd 13-GraphDatabaseNeo4j
docker compose up -d
```

This starts five containers, one per project. GraphModeling's is
`neo4j-modeling`, reachable at `bolt://localhost:7687` with credentials
`neo4j` / `graphmodeling` -- already what `GraphModeling/appsettings.json`
points at.

## Learning Goals
By completing this exercise, you will:
- Create nodes with labels and properties through parameterized Cypher,
  never string-concatenated
- Model a relationship (`WORKS_AT`) with its own properties, and explain
  why that's something a relational foreign key or an embedded document
  can't do as cleanly
- Choose and justify a directionality convention for a conceptually
  symmetric relationship (`KNOWS`), and contrast it with a genuinely
  directed one (`FOLLOWS`)
- Create uniqueness constraints and property indexes -- Cypher's DDL
  equivalent -- and explain what each one actually protects against
- Write an idempotent seed method using `MERGE`, and prove it's idempotent

---

## The Scenario

You're modeling a small professional network:
- **Person** (`id`, `name`)
- **Company** (`id`, `name`)
- **`WORKS_AT`** (Person → Company), with `role` and `since` properties
- **`FOLLOWS`** (Person → Person), directed and asymmetric by nature --
  Alice can follow Bob without Bob following her back
- **`KNOWS`** (Person → Person), conceptually symmetric -- but every
  Cypher relationship is directed, so Part 3 picks a convention

All the code in this exercise goes in your workspace, under
`GraphModeling/GraphModeling/` (relative to this file). Create the
`Domain/` and `Persistence/` folders as you go.

---

## Part 1: Nodes and Labels

A Neo4j node has one or more **labels** (`:Person`, `:Company`) and a set
of properties. There's no schema forcing every `Person` node to have the
same properties -- the database will happily let you create a `Person`
node with no `name` at all. Nothing enforces the shape except your own
code, which is exactly why the parameterized-write discipline in this
part matters from the very first line of Cypher you write.

### Step 1.1: The Domain Types

**Your Task:**
Create `Domain/Person.cs`:

```csharp
namespace GraphModeling.Domain;

public sealed record Person(string Id, string Name);
```

And `Domain/Company.cs`:

```csharp
namespace GraphModeling.Domain;

public sealed record Company(string Id, string Name);
```

`Id` here is a stable, application-assigned business key ("p1", "c1")
-- not a database-generated identity. Neo4j does have an internal node
id, but it's not meant to be a stable external reference (it can be
reused after a node is deleted), so every node type in this exercise
carries its own `id` property instead.

### Step 1.2: Writing Nodes, Safely

**Your Task:**
Create `Persistence/GraphWriter.cs`:

```csharp
using GraphModeling.Domain;
using Neo4j.Driver;

namespace GraphModeling.Persistence;

public static class GraphWriter
{
    public static async Task CreatePersonAsync(IAsyncSession session, Person person)
    {
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MERGE (p:Person {id: $id}) SET p.name = $name",
                new { id = person.Id, name = person.Name });
            await cursor.ConsumeAsync();
        });
    }

    public static async Task CreateCompanyAsync(IAsyncSession session, Company company)
    {
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MERGE (c:Company {id: $id}) SET c.name = $name",
                new { id = company.Id, name = company.Name });
            await cursor.ConsumeAsync();
        });
    }
}
```

**Why `$id` and `$name`, never string interpolation:** if this method
built its query as `$"MERGE (p:Person {{id: '{person.Id}'}}) ..."`, a
`Person.Name` of `Robert'); MATCH (n) DETACH DELETE n //` would run as a
*second Cypher statement* the moment it hit the database -- deleting
every node in the graph. This is exactly the same lesson as module 10's
raw-SQL warnings and module 11's Cosmos SQL API warnings, just for
Cypher: **untrusted values are parameters, never query text**, no matter
how convenient string interpolation looks for a "just this once" query.

**Why `MERGE` instead of `CREATE`:** `MERGE` matches an existing node
with that `id` if one exists, or creates it if not -- `CREATE` would
always insert a new node, even if one with the same `id` already
existed. You'll come back to why that specifically matters in Part 5.

### Step 1.3: Try It

**Your Task:**
Update `Program.cs`:

```csharp
using GraphModeling.Domain;
using GraphModeling.Persistence;
using Microsoft.Extensions.Configuration;
using Neo4j.Driver;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var uri = configuration["Neo4j:Uri"]!;
var username = configuration["Neo4j:Username"]!;
var password = configuration["Neo4j:Password"]!;

await using var driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
await driver.VerifyConnectivityAsync();

await using var session = driver.AsyncSession();

await GraphWriter.CreatePersonAsync(session, new Person("p1", "Alice Anderson"));
await GraphWriter.CreateCompanyAsync(session, new Company("c1", "Acme Robotics"));

Console.WriteLine("Created a Person node and a Company node.");
```

**Run it:**
```bash
dotnet run --project GraphModeling
```

Open the Neo4j Browser at `http://localhost:7474` (credentials `neo4j` /
`graphmodeling`) and run `MATCH (n) RETURN n` to see both nodes.

**Questions to think about:**
1. What would happen, concretely, if `CreatePersonAsync` used `CREATE`
   instead of `MERGE` and you ran `Program.cs` twice in a row?
2. Neo4j has no concept of a "table" -- a node's labels are the closest
   equivalent. What real difference is there between "a node with label
   `Person` and no `name` property" and "a row missing a required
   column" in a relational database?

---

## Part 2: Relationships as First-Class Data

This is the part of the graph model that has no clean equivalent in the
other two data models this repository has covered:

- **Module 10 (PostgreSQL, relational):** `Book.PublisherId` is a foreign
  key column. It can point at a `Publisher` row, and that's *all* it can
  do -- a foreign key column has no properties of its own. To attach a
  `role` and a `since` date to the fact that a book was published by a
  given publisher, you'd need an entirely separate junction table (like
  `BookGenre` from `EfCoreModeling`), just to give the relationship
  somewhere to keep its own data.
- **Module 11 (Cosmos DB, document):** an embedded `OrderLine[]` inside
  an `Order` document is data *about* a relationship, but it can't be
  queried or traversed independently of the `Order` that embeds it --
  there's no way to ask "show me every OrderLine across all orders where
  quantity > 10" without loading and scanning every order document.

In Neo4j, a relationship is a **first-class object**: it has a type
(`WORKS_AT`), it connects exactly two nodes, and it can carry its own
properties, queryable and traversable on their own terms, with no
junction table and no embedding required.

### Step 2.1: `WORKS_AT` with Properties

**Your Task:**
Add this method to `Persistence/GraphWriter.cs`:

```csharp
    public static async Task CreateWorksAtAsync(
        IAsyncSession session, string personId, string companyId, string role, int since)
    {
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person {id: $personId})
                MATCH (c:Company {id: $companyId})
                MERGE (p)-[r:WORKS_AT]->(c)
                SET r.role = $role, r.since = $since
                """,
                new { personId, companyId, role, since });
            await cursor.ConsumeAsync();
        });
    }
```

Notice `MERGE (p)-[r:WORKS_AT]->(c)` has **no properties inside the
relationship pattern**. That's deliberate: `MERGE` matches (or creates)
the relationship based only on its two endpoints and its type, and the
separate `SET` afterward updates `role`/`since` unconditionally. If you
put `{role: $role, since: $since}` inside the `MERGE` pattern instead,
a second call with a *different* `role` for the same person/company pair
would create a **second** `WORKS_AT` edge rather than update the first --
exactly the duplication Part 5's idempotent seeding depends on avoiding.

### Step 2.2: Try It

**Your Task:**
Add this to `Program.cs`, after the two `Create...Async` calls from
Part 1:

```csharp
await GraphWriter.CreateWorksAtAsync(session, "p1", "c1", "Engineering Lead", 2018);

var (role, since) = await session.ExecuteReadAsync(async tx =>
{
    var cursor = await tx.RunAsync(
        """
        MATCH (p:Person {id: $personId})-[r:WORKS_AT]->(c:Company {id: $companyId})
        RETURN r.role AS role, r.since AS since
        """,
        new { personId = "p1", companyId = "c1" });
    var record = await cursor.SingleAsync();
    return (
        Neo4j.Driver.ValueExtensions.As<string>(record["role"]),
        Neo4j.Driver.ValueExtensions.As<long>(record["since"]));
});

Console.WriteLine($"p1 WORKS_AT c1 as '{role}' since {since}.");
```

**Run it again** and confirm the role/since round-trip.

**Questions to think about:**
1. If you needed to record that Alice worked at two *different*
   companies over her career (with different `role`/`since` at each),
   would `WORKS_AT` as modeled here support that? What would break, if
   anything?
2. Why does `ExecuteReadAsync`'s delegate return a plain `(string, long)`
   tuple instead of the `IResultCursor` itself? (Hint: what happens to a
   cursor once its transaction closes?)

---

## Part 3: Directionality

Every relationship in Cypher has a direction -- `(a)-[:TYPE]->(b)` always
points from `a` to `b`. That's a fine match for a relationship that's
*genuinely* directed, and an awkward one for a relationship that
conceptually isn't.

### `FOLLOWS`: Genuinely Directed

Alice following Bob on a social network implies **nothing** about
whether Bob follows Alice back. There's no convention to invent here --
the direction the caller passes in is the direction that's meaningful.

**Your Task:**
Add this to `Persistence/GraphWriter.cs`:

```csharp
    public static async Task CreateFollowsAsync(IAsyncSession session, string followerId, string followeeId)
    {
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (follower:Person {id: $followerId})
                MATCH (followee:Person {id: $followeeId})
                MERGE (follower)-[:FOLLOWS]->(followee)
                """,
                new { followerId, followeeId });
            await cursor.ConsumeAsync();
        });
    }
```

### `KNOWS`: Conceptually Symmetric

If Alice knows Bob, Bob knows Alice, by definition -- there's no
"one-directional acquaintance." But Cypher gives you no "undirected
relationship" primitive. Two honest ways to model it:

1. **Two directed edges**, one each way. Reads can match either direction
   naturally, but every write has to create (or delete) both edges
   together -- and nothing stops them from drifting apart if some code
   path forgets the second write.
2. **One directed edge**, with a documented convention for which
   direction it's created in, and every query matching it with an
   **undirected pattern** (`(a)-[:KNOWS]-(b)`, no arrowhead) instead of a
   directed one.

This exercise picks option 2, with the convention: **always create the
edge from the person with the lexicographically smaller id.** That
halves the storage and removes the "did both writes happen" failure mode
entirely -- the trade-off is that every query touching `KNOWS` has to
remember to match it undirected, or it will silently see only half of a
given person's KNOWS edges (whichever half happens to have the smaller
id on the far side).

**Your Task:**
Create `Persistence/KnowsConvention.cs`:

```csharp
namespace GraphModeling.Persistence;

public static class KnowsConvention
{
    public static (string FromId, string ToId) Resolve(string personAId, string personBId)
    {
        if (personAId == personBId)
        {
            throw new ArgumentException("A person cannot KNOW themselves.");
        }

        return string.CompareOrdinal(personAId, personBId) < 0
            ? (personAId, personBId)
            : (personBId, personAId);
    }
}
```

And add this to `Persistence/GraphWriter.cs`:

```csharp
    public static async Task CreateKnowsAsync(IAsyncSession session, string personAId, string personBId, int since)
    {
        var (fromId, toId) = KnowsConvention.Resolve(personAId, personBId);

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $fromId})
                MATCH (b:Person {id: $toId})
                MERGE (a)-[r:KNOWS]->(b)
                SET r.since = $since
                """,
                new { fromId, toId, since });
            await cursor.ConsumeAsync();
        });
    }
```

Callers of `CreateKnowsAsync` never need to know or care which id is
smaller -- pass the two ids in whichever order reads naturally, and the
edge always ends up pointing the same way.

### Step 3.1: Try It

**Your Task:**
Add this to `Program.cs`:

```csharp
await GraphWriter.CreatePersonAsync(session, new Person("p9", "Ingrid Iversen"));
await GraphWriter.CreateFollowsAsync(session, "p1", "p9");

// Passed in "reverse" order on purpose:
await GraphWriter.CreateKnowsAsync(session, "p9", "p1", 2015);

var direction = await session.ExecuteReadAsync(async tx =>
{
    var cursor = await tx.RunAsync(
        """
        MATCH (a:Person)-[:KNOWS]->(b:Person)
        WHERE a.id IN ['p1', 'p9'] AND b.id IN ['p1', 'p9']
        RETURN a.id AS fromId, b.id AS toId
        """);
    var record = await cursor.SingleAsync();
    return $"{Neo4j.Driver.ValueExtensions.As<string>(record["fromId"])} -> {Neo4j.Driver.ValueExtensions.As<string>(record["toId"])}";
});

Console.WriteLine($"KNOWS(p9, p1) was requested but is stored as: {direction}");
```

**Run it** and confirm the output is `p1 -> p9`, even though you called
`CreateKnowsAsync(session, "p9", "p1", 2015)`.

**Questions to think about:**
1. If you queried `MATCH (:Person {id: 'p9'})-[:KNOWS]->(x) RETURN x`
   (directed, from p9), would it find p1? What about
   `MATCH (:Person {id: 'p9'})-[:KNOWS]-(x) RETURN x` (undirected)? Why
   the difference?
2. What real-world relationship in this domain would you model as *two*
   directed edges instead of picking a convention like `KNOWS` does? What
   makes it different?

---

## Part 4: Uniqueness Constraints and Property Indexes

Nothing so far stops you from calling `CreatePersonAsync` with the same
`id` twice under concurrent load and ending up with a race -- `MERGE`
matches-or-creates, but that guarantee is only as strong as the
database's own concurrency control, and without an index backing the
lookup, `MERGE` also has to scan every `Person` node to check whether
one with that `id` already exists. Constraints and indexes are Cypher's
answer to both problems -- the DDL equivalent of a relational primary key
and a regular index.

### Step 4.1: The Schema

**Your Task:**
Create `Persistence/GraphSchema.cs`:

```csharp
using Neo4j.Driver;

namespace GraphModeling.Persistence;

public static class GraphSchema
{
    public static async Task EnsureConstraintsAndIndexesAsync(IAsyncSession session)
    {
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "CREATE CONSTRAINT person_id_unique IF NOT EXISTS " +
                "FOR (p:Person) REQUIRE p.id IS UNIQUE");
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "CREATE CONSTRAINT company_id_unique IF NOT EXISTS " +
                "FOR (c:Company) REQUIRE c.id IS UNIQUE");
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "CREATE INDEX person_name_index IF NOT EXISTS " +
                "FOR (p:Person) ON (p.name)");
            await cursor.ConsumeAsync();
        });
    }
}
```

**Why the constraint, not just discipline:** the uniqueness constraint on
`Person.id` (and `Company.id`) is the graph analogue of a relational
primary key. Without it, `CREATE (p:Person {id: 'p1', ...})` run twice
(from two concurrent requests, say) would happily create **two distinct
nodes** that both claim to be "p1" -- and every relationship anyone ever
points at "p1" would nondeterministically land on whichever one it
happens to match first. `MERGE` reduces how *likely* that is, but the
constraint is what makes it a database-enforced guarantee instead of an
application-level convention that can be bypassed by any code path that
uses `CREATE` directly (including, deliberately, one of this project's
own tests).

**Why the index is a different kind of thing:** the `person_name_index`
enforces nothing at all -- it exists purely so `MATCH (p:Person {name:
$name})` doesn't have to scan every `Person` node in the graph to find
one. A constraint changes what's *possible*; an index only changes how
*fast* something already possible is.

### Step 4.2: Try It

**Your Task:**
Add this to `Program.cs`:

```csharp
await GraphSchema.EnsureConstraintsAndIndexesAsync(session);
Console.WriteLine("Ensured Person.id/Company.id uniqueness constraints and the Person.name index.");
```

**Run it**, then open the Neo4j Browser and run `SHOW CONSTRAINTS` and
`SHOW INDEXES` to see them listed.

**Questions to think about:**
1. `CREATE CONSTRAINT ... IF NOT EXISTS` makes this method safe to call
   every time the app starts. What would go wrong if you dropped
   `IF NOT EXISTS` and called this method twice?
2. Try (in the Neo4j Browser, not your code) running
   `CREATE (p:Person {id: 'p1', name: 'Duplicate'})` after Part 1 has
   already created a `p1` node. What error do you get, and which part of
   `GraphSchema` is responsible for it firing?

---

## Part 5: A Seed Dataset for the Shared Domain

Every project in this module assumes this same domain shape exists with
enough data to be interesting to traverse and analyze. This part builds
that dataset: ~14 people, 4 companies, and a deliberately uneven mesh of
`KNOWS`/`WORKS_AT`/`FOLLOWS` relationships -- a couple of well-connected
"hub" people, and a smaller cluster that isn't directly tied to either
hub.

### Step 5.1: The Seeder

**Your Task:**
Create `Persistence/GraphSeeder.cs`. Start with the data:

```csharp
using GraphModeling.Domain;
using Neo4j.Driver;

namespace GraphModeling.Persistence;

public static class GraphSeeder
{
    private static readonly Person[] People =
    [
        new("p1", "Alice Anderson"), new("p2", "Bob Brennan"), new("p3", "Carla Chen"),
        new("p4", "David Diaz"), new("p5", "Elena Evans"), new("p6", "Frank Foster"),
        new("p7", "Grace Gomez"), new("p8", "Hassan Hussain"), new("p9", "Ingrid Iversen"),
        new("p10", "Jamal Jackson"), new("p11", "Katrin Kessler"), new("p12", "Liam Lopez"),
        new("p13", "Maya Martin"), new("p14", "Noah Nguyen"),
    ];

    private static readonly Company[] Companies =
    [
        new("c1", "Acme Robotics"), new("c2", "Globex Analytics"),
        new("c3", "Initech Software"), new("c4", "Umbrella Health"),
    ];

    private static readonly (string PersonId, string CompanyId, string Role, int Since)[] WorksAt =
    [
        ("p1", "c1", "Engineering Lead", 2018), ("p2", "c1", "Senior Engineer", 2019),
        ("p3", "c1", "Product Manager", 2020), ("p12", "c1", "Intern", 2023),
        ("p4", "c2", "Data Scientist", 2017), ("p5", "c2", "ML Engineer", 2021),
        ("p6", "c2", "Analytics Director", 2016), ("p13", "c2", "Sales Representative", 2019),
        ("p7", "c3", "Software Engineer", 2019), ("p8", "c3", "QA Engineer", 2020),
        ("p9", "c3", "CTO", 2015), ("p14", "c3", "DevOps Engineer", 2021),
        ("p10", "c4", "Research Scientist", 2018), ("p11", "c4", "Lab Technician", 2022),
    ];

    // Conceptually symmetric -- listed in whatever order reads naturally;
    // GraphWriter.CreateKnowsAsync normalizes the actual edge direction.
    private static readonly (string PersonAId, string PersonBId, int Since)[] Knows =
    [
        ("p1", "p2", 2018), ("p1", "p3", 2019), ("p1", "p4", 2020), ("p1", "p6", 2021),
        ("p1", "p9", 2015), ("p1", "p12", 2023), ("p9", "p7", 2019), ("p9", "p8", 2020),
        ("p9", "p14", 2021), ("p9", "p6", 2016), ("p2", "p3", 2019), ("p3", "p12", 2023),
        ("p4", "p5", 2021), ("p7", "p8", 2019), ("p10", "p11", 2022), ("p13", "p6", 2019),
    ];

    // p1 and p9 are followed by many and follow few -- an "influencer"
    // shape later projects' PageRank exercise depends on.
    private static readonly (string FollowerId, string FolloweeId)[] Follows =
    [
        ("p2", "p1"), ("p3", "p1"), ("p4", "p1"), ("p5", "p1"), ("p7", "p1"),
        ("p10", "p1"), ("p13", "p1"), ("p1", "p9"), ("p1", "p6"), ("p6", "p9"),
        ("p8", "p9"), ("p14", "p9"), ("p12", "p3"), ("p11", "p10"), ("p5", "p4"), ("p3", "p2"),
    ];

    public static async Task SeedAsync(IAsyncSession session)
    {
        foreach (var person in People)
            await GraphWriter.CreatePersonAsync(session, person);

        foreach (var company in Companies)
            await GraphWriter.CreateCompanyAsync(session, company);

        foreach (var (personId, companyId, role, since) in WorksAt)
            await GraphWriter.CreateWorksAtAsync(session, personId, companyId, role, since);

        foreach (var (personAId, personBId, since) in Knows)
            await GraphWriter.CreateKnowsAsync(session, personAId, personBId, since);

        foreach (var (followerId, followeeId) in Follows)
            await GraphWriter.CreateFollowsAsync(session, followerId, followeeId);
    }
}
```

**Why this is idempotent "for free":** `SeedAsync` calls nothing but the
`GraphWriter` methods you already wrote in Parts 1-3, and every one of
them is built on `MERGE`. Running `SeedAsync` a second time re-applies
the same `id`s and the same relationship patterns -- `MERGE` finds each
node and edge already there and just re-`SET`s the same properties.
Nothing new gets created. That's a direct consequence of decisions you
already made in earlier parts, not new logic this part had to add.

### Step 5.2: Try It, Twice

**Your Task:**
Add this to `Program.cs`:

```csharp
await GraphSeeder.SeedAsync(session);

var personCount = await session.ExecuteReadAsync(async tx =>
{
    var cursor = await tx.RunAsync("MATCH (p:Person) RETURN count(p) AS c");
    var record = await cursor.SingleAsync();
    return Neo4j.Driver.ValueExtensions.As<long>(record["c"]);
});

Console.WriteLine($"Seeded the shared domain -- {personCount} Person nodes.");
```

**Run it twice in a row** (`dotnet run --project GraphModeling`, twice).
The printed count should be identical both times.

**Questions to think about:**
1. `WorksAt`, `Knows`, and `Follows` are all arrays of tuples, iterated
   with a plain `foreach`, one driver round-trip per row. For 14 people
   and ~46 relationships that's fine -- at what rough scale would you
   reach for Cypher's `UNWIND` (sending the whole list as one parameter
   and letting a single query iterate it) instead?
2. The seed data makes `p1` and `p9` hub-like (many `KNOWS` edges, many
   incoming `FOLLOWS`) while `p10`/`p11` form a small side cluster. Why
   does that unevenness matter for a *later* project doing shortest-path
   or PageRank work, versus a graph where every node has roughly the
   same number of connections?

---

## Checking Your Work

The reference solution in `../solution/` implements everything above,
plus `../tests/` has real tests (against a throwaway Testcontainers-based
Neo4j instance, not the `docker compose` one) that check constraint/index
existence, relationship property round-trips, the `KNOWS` direction
convention holding across every seeded edge, and seed idempotency.

```bash
dotnet test ../tests
```

This needs Docker running (Testcontainers starts its own container), but
**not** `docker compose up -d` first -- that's only for running your own
`Program.cs` interactively. The tests point at `solution/` by default;
to check **your** work instead, edit the `ProjectReference` in
`tests/GraphModeling.Tests.csproj` to point at
`../GraphModeling/GraphModeling.csproj`.

---

## Reflection Questions

1. **Relationships as data**: name one query you could answer easily with
   `WORKS_AT` as a first-class relationship that would require an extra
   join table in module 10's relational model, or an extra
   cross-document scan in module 11's document model.
2. **Directionality conventions**: `KNOWS` picked "lexicographically
   smaller id first." What's a different, equally valid convention you
   could have picked instead, and what would change about the queries
   that read it?
3. **Constraints vs. indexes**: in one sentence each, what does
   `person_id_unique` make *impossible*, and what does
   `person_name_index` make *fast*? Could a single Cypher statement do
   both at once?
4. **Idempotent seeding**: `SeedAsync` is idempotent because every method
   it calls uses `MERGE`. If even one of those methods had used `CREATE`
   instead, which specific test in `tests/SeedDataTests.cs` would start
   failing, and why?
5. **Hub-and-cluster structure**: why does a seed dataset built for a
   *learning* module deliberately avoid making every node equally
   connected to every other node?

---

## Summary

You've learned:
- Creating nodes with labels and properties via parameterized Cypher
- Modeling a relationship with its own properties (`WORKS_AT`), and why
  that's not achievable as cleanly with a relational foreign key or an
  embedded document
- Choosing and documenting a directionality convention for a
  conceptually symmetric relationship (`KNOWS`), versus a genuinely
  directed one (`FOLLOWS`)
- Creating uniqueness constraints and property indexes, and the
  difference between what each one guarantees
- Writing an idempotent seed method built entirely on `MERGE`

## Next Steps

Continue to **GraphQuerying** to explore Cypher's query side in depth --
pattern matching, aggregation, and pagination -- against this same
domain (seeded independently, in its own container).
