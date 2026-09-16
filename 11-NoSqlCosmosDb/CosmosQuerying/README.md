# CosmosQuerying - Querying Azure Cosmos DB

## Overview
Modeling a document and choosing a partition key gets you a container. This
project is about getting data back out of it well: the LINQ provider,
parameterized SQL, partition-aware queries, efficient pagination, and
projections that cut request-unit (RU) cost, not just payload size.
Everything runs against the real Azure Cosmos DB emulator, orchestrated by
.NET Aspire -- RU charges and partition behavior are the whole point, and
neither exists in an in-memory substitute.

## What You'll Learn

### Core Querying
- **The LINQ Provider**: `GetItemLinqQueryable<T>()`, and how it differs
  from EF Core's LINQ provider (Cosmos SQL, not T-SQL; a narrower set of
  translatable operators; no cross-container joins)
- **The SQL API**: `QueryDefinition` with `WithParameter`, and why that's
  the only safe way to build a Cosmos SQL query from external input
- **Partitioning**: cross-partition vs. partition-scoped queries via
  `QueryRequestOptions.PartitionKey`, and the RU cost difference
- **Pagination**: `FeedIterator<T>` and continuation tokens vs. `Skip`/`Take`
- **Projections**: narrowing the `SELECT` list to reduce RU cost, verified
  against `RequestCharge`

### The Sharp Edges
- **The camelCase trap**: the Cosmos SDK's document serializer and its LINQ
  query translator are two independent settings -- get only one right and
  LINQ queries silently return zero rows
- **SQL injection, Cosmos-flavored**: `QueryDefinition` + `WithParameter` is
  the same lesson EfCoreQuerying (module 10) teaches for
  `FromSqlInterpolated`, applied to Cosmos's own SQL dialect

## Why This Matters

### Real-World Scenarios

**Partition-scoped vs. cross-partition**:
```csharp
// Cosmos fans this out across every physical partition and merges results.
var query = new QueryDefinition("SELECT * FROM c WHERE c.customerId = @id").WithParameter("@id", id);
using var iterator = orders.GetItemQueryIterator<Order>(query);

// The SAME query, scoped to one partition -- no fan-out, no merge step.
var options = new QueryRequestOptions { PartitionKey = new PartitionKey(id) };
using var scoped = orders.GetItemQueryIterator<Order>(query, requestOptions: options);
```

**Safe parameterized SQL**:
```csharp
// The @status placeholder becomes a real query parameter -- not string
// concatenation, so external input can never restructure the query.
var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status").WithParameter("@status", status);
```

**Projection for RU savings, not just smaller payloads**:
```csharp
// Narrowing the SELECT list reduces what Cosmos reads server-side --
// fewer RU for the SAME logical query, measured via response.RequestCharge.
var query = new QueryDefinition("SELECT c.id, c.customerId, c.totalAmount FROM c WHERE c.status = @status")
    .WithParameter("@status", status);
```

## Project Structure

```
CosmosQuerying/
├── CosmosQuerying/          # <- YOUR WORKSPACE. Write your code here.
│   ├── CosmosQuerying.csproj  #   ready to build
│   ├── Program.cs             #   replace as you work through Part 0
│   ├── appsettings.json       #   already points at the local emulator
│   ├── Domain/                #   empty, waiting for Customer/Order/OrderLine
│   ├── Persistence/            #   empty, waiting for your serialization + init helpers
│   ├── Seed/                    #   empty, waiting for your seeder
│   ├── Dtos/                     #   empty, waiting for your projection DTOs
│   └── Demos/                     #   empty, waiting for your per-part demos
├── solution/                # <- REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Domain/                 Customer, Order, OrderLine
│   ├── Persistence/             CosmosSerialization, CosmosInitializer
│   ├── Seed/                     OrderSeeder (deterministic: 25 customers, 180 orders)
│   ├── Dtos/                      OrderSummaryDto
│   └── Demos/                      one runnable demo per part (1-5)
├── AppHost/                 # .NET Aspire orchestration -- starts the Cosmos DB emulator
├── tests/                   # ~20 tests proving the behavior, against the real emulator
├── EXERCISE.md
├── GETTING_STARTED.md
└── README.md
```

## Quick Start

1. **Navigate:**
   ```bash
   cd 11-NoSqlCosmosDb/CosmosQuerying
   ```

2. **Verify:**
   ```bash
   dotnet build CosmosQuerying
   ```

3. **Start learning:**
   Open [EXERCISE.md](EXERCISE.md)

## The Learning Path

### Part 0: Domain Model, Serialization, Containers, and Seed Data (45 min)
Set up the two containers, fix the camelCase serialization trap up front,
and seed 180 deterministic orders across 25 customers.

### Part 1: The LINQ Provider (25 min)
`GetItemLinqQueryable<T>()`, and how its translation target makes it a
narrower tool than EF Core's LINQ provider.

### Part 2: The SQL API with QueryDefinition (20 min)
Parameterized Cosmos SQL, and why that's the only safe way to build a query.

### Part 3: Cross-Partition vs. Partition-Scoped Queries (30 min)
The same query, run two ways, with a real RU cost difference to observe.

### Part 4: Pagination (30 min)
`FeedIterator<T>` and continuation tokens vs. `Skip`/`Take`.

### Part 5: Projections (25 min)
Narrowing the `SELECT` list to reduce RU cost, proven with `RequestCharge`.

**Total Time**: 3-4 hours

## Best Practices

### Queries
✅ Pass `CosmosSerialization.LinqOptions` to every `GetItemLinqQueryable<T>()`
   call, and `CosmosSerialization.ClientOptions` to your `CosmosClient`
✅ Always build Cosmos SQL with `QueryDefinition` + `WithParameter`
❌ Never interpolate or concatenate a value into Cosmos SQL text

### Partitioning
✅ Set `QueryRequestOptions.PartitionKey` whenever the partition key value
   is known up front
❌ Don't run a cross-partition query when a partition-scoped one would do

### Pagination
✅ Use `FeedIterator<T>` + continuation tokens for deep or repeated paging
❌ Don't reach for `Skip`/`Take` on a page far into a large result set

### Projections
✅ Narrow the `SELECT` list for read-only queries and measure the RU saving
   via `RequestCharge`
❌ Don't assume a projection helps without checking -- verify it

## Testing Considerations

The tests in `../tests` run against the real Cosmos DB emulator, started
once per test run by .NET Aspire (`Aspire.Hosting.Testing`) -- the emulator
takes minutes to boot the first time, which is why the shared test fixture
starts it exactly once and every test class reuses the same instance. This
matters specifically for this project: RU charges, partition fan-out
behavior, and continuation tokens are all real Cosmos service behavior with
no faithful in-memory substitute.

## Next Steps

After completing CosmosQuerying:

1. **Review your own query code** -- which of your Cosmos queries reach for
   `SELECT *` when a projection would do? Which ones run cross-partition
   when the partition key was actually known?
2. **Explore the rest of this module** for data modeling, change feed, and
   partitioning/consistency topics this project didn't cover.

## Checklist

After this project, you should be able to:

- [ ] Query with `GetItemLinqQueryable<T>()` and explain how it differs from
      EF Core's LINQ provider
- [ ] Write parameterized Cosmos SQL with `QueryDefinition`/`WithParameter`
- [ ] Explain why interpolating a value into Cosmos SQL text is unsafe
- [ ] Scope a query to a single partition with `QueryRequestOptions.PartitionKey`
- [ ] Explain the RU cost difference between a cross-partition and a
      partition-scoped query
- [ ] Page through a large result set with `FeedIterator<T>` and
      continuation tokens
- [ ] Explain why `Skip`/`Take` gets costlier the deeper you page
- [ ] Project a narrow field list and prove the RU saving via `RequestCharge`

---

**Ready to query?** Open [EXERCISE.md](EXERCISE.md)!
