# GraphModeling - Nodes, Relationships, and Properties in Neo4j

## Overview
Modules 10 and 11 modeled the same kind of thing -- entities and the
connections between them -- as a normalized relational schema
(`EfCoreModeling`) and as documents with embedded or referenced data
(`CosmosModeling`). This project is module 13's first: the same kind of
modeling problem, solved as a **property graph**. The domain here is
deliberately small (a professional network: people, companies, who works
where, who knows whom, who follows whom) so the modeling *decisions* --
not the data volume -- are the whole point, and every other project in
this module reuses this exact domain shape.

Everything here runs against a real Neo4j instance through the official
`Neo4j.Driver` for .NET. There's no in-memory substitute that would teach
you anything real about Cypher, constraints, or relationship
directionality.

## What You'll Learn

### Graph Modeling
- **Nodes and labels**: `Person` and `Company` as labeled nodes with
  properties, created through parameterized Cypher -- never
  string-concatenated
- **Relationships as first-class data**: `WORKS_AT` carries its own
  `role`/`since` properties directly on the edge, something neither a
  relational foreign key column nor an embedded document array can do as
  cleanly
- **Directionality by design**: `FOLLOWS` is genuinely directed;
  `KNOWS` is conceptually symmetric but modeled as one directed edge
  under a documented convention (lexicographically smaller id first),
  traded off against always querying it with an undirected pattern
- **Uniqueness constraints and property indexes**: Cypher's DDL
  equivalent of a primary key and a regular index
- **Idempotent seeding**: a `MERGE`-based seed method safe to re-run any
  number of times

## Why This Matters

### A relational foreign key vs. a first-class relationship
```csharp
// Module 10 (EF Core / PostgreSQL): Book references Author by AuthorId.
// The foreign key column can point at a row. That's all it can do -- to
// attach properties to the relationship ITSELF, you need a separate
// junction table (see EfCoreModeling's BookGenre).
public class Book
{
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;
}

// This module: WORKS_AT is the relationship, and it carries role/since
// directly -- no junction table, independently queryable and traversable.
await session.ExecuteWriteAsync(async tx =>
{
    var cursor = await tx.RunAsync(
        """
        MATCH (p:Person {id: $personId}) MATCH (c:Company {id: $companyId})
        MERGE (p)-[r:WORKS_AT]->(c)
        SET r.role = $role, r.since = $since
        """,
        new { personId, companyId, role, since });
    await cursor.ConsumeAsync();
});
```

### Directionality is a modeling decision, not a database default
```cypher
// FOLLOWS is genuinely directed -- Alice following Bob says nothing
// about whether Bob follows Alice.
MERGE (follower)-[:FOLLOWS]->(followee)

// KNOWS is conceptually symmetric. This module picks ONE convention
// (smaller id first) and documents it, rather than either forgetting
// the direction matters or maintaining two edges that can drift apart:
MERGE (a)-[:KNOWS]->(b)   // where a.id < b.id, always
// ...and every read matches it undirected: (a)-[:KNOWS]-(b)
```

### A constraint changes what's possible; an index only changes what's fast
```cypher
// Without this, two concurrent writes could create two distinct nodes
// that both claim to be "p1" -- a graph-native primary-key violation.
CREATE CONSTRAINT person_id_unique IF NOT EXISTS
FOR (p:Person) REQUIRE p.id IS UNIQUE;

// This enforces nothing -- it just makes "find the person named X" fast
// instead of a full label scan.
CREATE INDEX person_name_index IF NOT EXISTS
FOR (p:Person) ON (p.name);
```

## Project Structure

```
GraphModeling/
├── GraphModeling/           # <- YOUR WORKSPACE. Write your code here.
│   ├── GraphModeling.csproj   #   ready to build
│   ├── Program.cs              #   replace as you work through EXERCISE.md
│   ├── appsettings.json        #   already points at neo4j-modeling's container
│   ├── Domain/                 #   create as you go: Person, Company
│   └── Persistence/             #   create as you go: GraphWriter, GraphSchema,
│                                 #   KnowsConvention, GraphSeeder
├── solution/                # <- REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Domain/                 Person, Company
│   ├── Persistence/             GraphWriter, GraphSchema, KnowsConvention, GraphSeeder
│   └── Program.cs                runs all 5 parts against the real container
├── tests/                   # 22 tests against a throwaway Testcontainers Neo4j
│   ├── Neo4jSharedFixture.cs    one container shared across all test classes
│   ├── SchemaTests.cs           constraints/indexes exist and are enforced
│   ├── NodeAndRelationshipTests.cs  nodes, WORKS_AT properties, FOLLOWS/KNOWS direction
│   ├── SeedDataTests.cs         seed shape, idempotency, hub/cluster structure
│   └── SmokeTests.cs            the original connectivity smoke test
├── EXERCISE.md
├── GETTING_STARTED.md
└── README.md
```

## Quick Start

1. **Start this module's containers** (from `13-GraphDatabaseNeo4j/`):
   ```bash
   docker compose up -d
   ```

2. **Navigate:**
   ```bash
   cd 13-GraphDatabaseNeo4j/GraphModeling
   ```

3. **Verify the build (no Docker needed for this step):**
   ```bash
   dotnet build
   ```

4. **Start learning:**
   Open [EXERCISE.md](EXERCISE.md)

## The Learning Path

### Part 1: Nodes and Labels (25 min)
Create `Person` and `Company` nodes through parameterized Cypher, and
understand why `MERGE` (not `CREATE`) is the right primitive from the
start.

### Part 2: Relationships as First-Class Data (30 min)
Model `WORKS_AT` with `role`/`since` properties living directly on the
edge -- and see exactly what that buys you over a relational foreign key
or an embedded document array.

### Part 3: Directionality (35 min)
Contrast `FOLLOWS` (genuinely directed) with `KNOWS` (conceptually
symmetric), pick and document a convention for the latter, and
understand the query-side trade-off that convention implies.

### Part 4: Uniqueness Constraints and Property Indexes (25 min)
Add `Person.id`/`Company.id` uniqueness constraints and a `Person.name`
index -- Cypher's DDL equivalent -- and see the constraint actually
reject a duplicate.

### Part 5: A Seed Dataset for the Shared Domain (40 min)
Build a ~14-person, 4-company, ~46-relationship seed dataset with hub
nodes and a smaller cluster, entirely through the `MERGE`-based methods
from Parts 1-3 -- and prove it's idempotent.

**Total Time**: 2.5-3 hours

## Best Practices

### Writing Cypher
✅ Always pass values as parameters (`$id`, `$name`) -- never build query
   text with string interpolation
✅ Use `MERGE` for anything that should be idempotent; reserve `CREATE`
   for cases where a genuine duplicate is intentional (or a bug you want
   a constraint to catch)
❌ Don't put properties inside a relationship's `MERGE` pattern unless a
   *different* value for that property should mean "this is a different
   relationship" -- otherwise use `SET` after the `MERGE`

### Directionality
✅ For a relationship that's genuinely directed, let the direction the
   caller passes in be the direction that's stored
✅ For a conceptually symmetric relationship, pick ONE convention, document
   it, and make every write and every read go through code that respects
   it
❌ Don't maintain two directed edges for a symmetric relationship unless
   you have a specific reason to query both directions independently --
   it doubles your write surface for no modeling benefit here

### Constraints and Indexes
✅ Add a uniqueness constraint on every node's business-key property
   before writing any seed data that assumes it holds
✅ Add an index on any property you'll look nodes up by, if it isn't
   already the target of a uniqueness constraint (which creates a backing
   index automatically)
❌ Don't rely on `MERGE` alone to prevent duplicates under concurrent
   writes -- it reduces the odds, the constraint is what makes it
   impossible

## Testing Considerations

The tests in `tests/` run against a **real, throwaway Neo4j container**
started by Testcontainers -- not the `docker compose` container this
`EXERCISE.md` has you use interactively. A single container is started
once and shared across every test class in the project (via an xUnit
collection fixture, `Neo4jSharedFixture`), with the schema and seed data
applied once during fixture setup. `dotnet test` needs Docker running,
but does **not** need `docker compose up -d` first.

## Next Steps

After completing GraphModeling:

1. **Review your directionality decisions** -- for `KNOWS` and `FOLLOWS`,
   say out loud which queries each choice makes natural and which it
   makes awkward.
2. **Move to GraphQuerying**
   - [../GraphQuerying](../GraphQuerying/)
   - Cypher fundamentals in depth: pattern matching, variable-length
     relationships, aggregation, and pagination against this same domain

## Checklist

After this project, you should be able to:

- [ ] Create labeled nodes with properties through parameterized Cypher
- [ ] Explain what a relationship-with-properties can do that a
      relational foreign key or an embedded document can't
- [ ] Choose and justify a directionality convention for a conceptually
      symmetric relationship, and state its query-side trade-off
- [ ] Create a uniqueness constraint and a property index, and explain
      the difference between what each one does
- [ ] Write an idempotent seed method using `MERGE`, and explain why it's
      idempotent without any extra "have I already seeded this" check

---

**Ready to model a graph?** Open [EXERCISE.md](EXERCISE.md)!
