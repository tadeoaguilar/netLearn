# CosmosModeling - Documents and Partition Keys in Azure Cosmos DB

## Overview
Module 10 (`EfCoreQuerying` and its siblings) taught a normalized relational schema:
`Author`, `Publisher`, `Book`, `Review`, connected by foreign keys and reassembled at query
time with `Include`. This project is the first of module 11's NoSQL counterpart, using an
equivalent domain -- customers and their orders -- to make the contrast concrete: no
foreign keys, no joins, no schema migrations. Instead, a document database asks you to make
a handful of upfront decisions -- what shape each document is, what to embed versus what to
put in its own container, and which property to partition by -- and every one of those
decisions has a real cost/benefit trade-off attached, not a "correct" answer independent of
how the data is actually used.

Everything here runs against the real Cosmos DB emulator, orchestrated by **.NET Aspire**
rather than Docker Compose (module 10's approach). There's no in-memory substitute that
would teach you anything real about partition keys or Request Unit (RU) cost.

## What You'll Learn

### Document Modeling
- **Choosing a partition key**: `/id` for a point-lookup-dominated document (`Customer`),
  `/customerId` for a document dominated by "give me this parent's children" queries
  (`Order`), and the storage/throughput trade-off that comes with the latter
- **Embedding vs. referencing**: when related data belongs inside its parent document
  (`OrderLine[]` inside `Order`) versus in its own container referenced by id (a
  hypothetical `Product` catalog)
- **Schema evolution without migrations**: adding a field to a document type and handling
  both the old (field absent) and new (field present) shapes in the same reading code
- **Synthetic/composite partition keys**: combining two properties into one partition key
  string to bound the growth of an unusually high-volume partition

### Aspire Orchestration
- An `AppHost` that starts the Cosmos DB Linux emulator in a container and provisions the
  database and containers this project needs
- Aspire-based integration tests (`Aspire.Hosting.Testing`) that boot the same `AppHost`
  in-process and assert against the real emulator

## Why This Matters

### A relational foreign key vs. an embedded document
```csharp
// Module 10 (EF Core / PostgreSQL): Book references Author by AuthorId, a separate row in
// a separate table, reassembled with a join at query time.
public class Book
{
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;
}

// This module: an Order's line items are embedded directly -- there is no "OrderLines"
// table to join against, because there's no relational engine doing the joining. The whole
// order, lines included, is one document, one read, one write.
public class Order
{
    public List<OrderLine> OrderLines { get; set; } = [];
}
```

### A partition key is a query-shape decision, not a schema decision
```csharp
// /customerId makes "this customer's orders" a single-partition query...
var query = orders
    .GetItemLinqQueryable<Order>(requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(customerId) })
    .Where(o => o.CustomerId == customerId);

// ...but it means every order that customer EVER places lives in the same logical
// partition -- fine for almost everyone, a real problem for one extremely active account.
// Part 5 shows the synthetic-key fix for exactly that case.
```

### Schema evolution: two shapes, one container, no migration
```csharp
// An order written 6 months ago never had ShippingAddress. Reading it today doesn't throw
// -- Newtonsoft just leaves the property at its default (null). Your code has to decide
// what "missing" means, deliberately:
var shippingLine = order.ShippingAddress is { } address
    ? $"{address.Line1}, {address.City}"
    : "(no shipping address on file -- this order pre-dates that field)";
```

## Project Structure

```
CosmosModeling/
├── CosmosModeling/          # <- YOUR WORKSPACE. Write your code here.
│   ├── CosmosModeling.csproj  #   ready to build
│   ├── Program.cs              #   replace as you work through Part 0
│   ├── appsettings.json        #   already points at the emulator's well-known endpoint/key
│   ├── Domain/                 #   create as you go: Customer, Order, OrderLine, ...
│   ├── Persistence/             #   create as you go: CosmosInitializer
│   └── Demos/                    #   create as you go: one demo per part
├── solution/                # <- REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Domain/                Customer, Order, OrderLine, ShippingAddress, HighVolumeOrder
│   ├── Persistence/            CosmosInitializer
│   └── Demos/                   one runnable demo per part
├── AppHost/                 # .NET Aspire orchestration: starts the Cosmos emulator,
│   │                          provisions the database/containers, runs `solution`
│   └── Program.cs
├── tests/                   # ~19 tests against the real emulator (via Aspire.Hosting.Testing)
├── EXERCISE.md
├── GETTING_STARTED.md
└── README.md
```

## Quick Start

1. **Make sure Docker is running.** The Cosmos DB emulator is a Linux container Aspire
   starts for you -- there's nothing to `docker compose up` yourself.

2. **Navigate:**
   ```bash
   cd 11-NoSqlCosmosDb/CosmosModeling
   ```

3. **Verify the build (no Docker needed for this step):**
   ```bash
   dotnet build
   ```

4. **Start learning:**
   Open [EXERCISE.md](EXERCISE.md)

## The Learning Path

### Part 0: Project Setup (15 min)
Create the folders you'll fill in, and understand what's already wired up in `AppHost`.

### Part 1: The Customer Document and Its Partition Key (30 min)
Model `Customer`, choose `/id` as its partition key, and see why that makes every lookup a
cheap single-partition point read.

### Part 2: The Order Document With Embedded Order Lines (40 min)
Model `Order` with an embedded `OrderLine[]`, choose `/customerId`, and confront the
storage/throughput trade-off of one very active customer's partition growing forever.

### Part 3: Embedding vs. Referencing (30 min)
Contrast the embedded `OrderLine[]` (right here) against a hypothetical `Product` catalog
(wrong to embed) -- and articulate the general rule for telling the two cases apart.

### Part 4: Schema Evolution Without Migrations (35 min)
Add a field to `Order`, write an old-shape document directly as raw JSON, and prove your
reading code handles both shapes without a migration step existing anywhere.

### Part 5: A Synthetic/Composite Partition Key (35 min)
Build `customerId:yyyy-MM` as a partition key for a hypothetical high-volume customer, and
see exactly what it costs you in exchange for the growth it bounds.

**Total Time**: 3-4 hours

## Best Practices

### Partition Keys
✅ Choose the property your dominant query filters by
✅ Always set `QueryRequestOptions.PartitionKey` when the query already knows it -- it's
   the difference between a single-partition and a cross-partition query
❌ Don't pick a partition key with low cardinality (e.g. a fixed constant, or a `Status`
   with three possible values) -- every distinct value becomes, at most, one partition

### Embedding vs. Referencing
✅ Embed data that's small, unshared, and always read/written with its parent
✅ Reference data that's large, shared across many parents, or independently
   queried/updated
❌ Don't embed a catalog-style entity (products, categories) just because it's convenient
   at write time -- the update-fan-out cost shows up later

### Schema Evolution
✅ Make new fields nullable (or give them safe defaults) so old documents deserialize
   cleanly
✅ Use a `SchemaVersion` convention when "is this field present" isn't a reliable enough
   signal on its own
❌ Don't assume every document in a container has the same shape -- write reading code
   that checks, not code that assumes

## Testing Considerations

The tests in `tests/` run against the **real Cosmos DB emulator**, started once for the
whole test run via `Aspire.Hosting.Testing` (the same `AppHost` project, booted
in-process) and shared across every test class through a collection fixture. This matters
specifically here: the emulator's cold start is measured in **minutes**, not seconds, so
starting a fresh one per test (the pattern module 10's Postgres/Testcontainers fixture
uses, where a fresh container per run is cheap) would make the suite impractically slow.
`dotnet test` needs Docker running, but does not need anything started by hand first.

## Next Steps

After completing CosmosModeling:

1. **Review your partition key choices** — for each container, name the query it makes
   cheap and the query it makes expensive, out loud.
2. **Move to CosmosQuerying**
   - [../CosmosQuerying](../CosmosQuerying/)
   - The LINQ provider and the parameterized SQL API against this same domain, with more
     data and a closer look at cross-partition query cost

## Checklist

After this project, you should be able to:

- [ ] Choose a partition key based on a document's dominant access pattern
- [ ] Explain the storage/throughput trade-off of a `/customerId`-style partition key
- [ ] Decide whether to embed or reference related data, and justify the choice
- [ ] Write code that tolerates two different shapes of the same document type
- [ ] Use a `SchemaVersion` convention deliberately, not as a substitute for null checks
- [ ] Build and use a synthetic, composite partition key
- [ ] Explain why an Aspire `AppHost` is orchestration, not a hard dependency of the app

---

**Ready to model some documents?** Open [EXERCISE.md](EXERCISE.md)!
