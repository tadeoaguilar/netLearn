# CosmosIndexingAndThroughput - Cost and Performance in Azure Cosmos DB

## Overview
The rest of module 11 asks how to model and query documents. This project asks what all of
that actually **costs**: Cosmos DB indexes every property of every document by default,
bills every single operation in Request Units (RUs), and offers two genuinely different ways
to provision throughput. None of that is optional background reading here -- it's measured,
against the same retail-orders domain (`Customer`, `Order` with embedded `OrderLine[]`) the
rest of the module uses, seeded with ~120 orders so the cost differences are large enough to
actually see.

Everything here runs against the real Cosmos DB emulator, orchestrated by **.NET Aspire**.
RU cost and indexing behavior aren't things an in-memory fake could teach you anything real
about.

## What You'll Learn

### Indexing
- Cosmos DB's **default indexing policy** indexes every property automatically, including
  every element of an embedded array -- and what that costs on every write to a document
  with a large embedded array you never query into
- How to **exclude a subtree** from indexing, and the write-cost savings versus query-cost
  trade-off that comes with it
- **Composite indexes**: why Cosmos requires one to sort by two or more properties in a
  single `ORDER BY`, and how to add one

### Cost Measurement
- Reading `RequestCharge` off point reads, single-partition queries, cross-partition
  queries, and writes, to see their real relative cost against your own data -- not just a
  claim from documentation

### Throughput
- **Manual** (fixed RU/s, predictable cost and a hard ceiling) vs. **autoscale** (a
  configured max RU/s that scales down to 10% automatically), and which `ThroughputProperties`
  factory method configures each
- Why container-level indexing policy and throughput are configured from the **client SDK**
  here, not the Aspire `AppHost` -- the hosting integration's `AddContainer` doesn't expose
  either

### Resilience
- Catching `CosmosException` for `429 TooManyRequests`, reading `RetryAfter`, and
  implementing your own backoff-and-retry
- What the Cosmos SDK's own built-in retry (`MaxRetryAttemptsOnRateLimitedRequests` /
  `MaxRetryWaitTimeOnRateLimitedRequests`) already covers, and when manual retry logic on
  top of it is still worth writing

## Why This Matters

### An embedded array is not free to index
```csharp
// Every property here, including every element of OrderLines, is indexed automatically
// under Cosmos's default policy -- whether or not any query ever filters on it.
public class Order
{
    public List<OrderLine> OrderLines { get; set; } = [];
}

// Excluding it from the indexing policy stops every write from paying to maintain an
// index term per line item, for a subtree this app never queries into directly:
policy.ExcludedPaths.Add(new ExcludedPath { Path = "/orderLines/*" });
```

### Sorting by two properties needs a composite index
```csharp
// Fails with a 400 until a matching composite index exists -- single-property sorts never
// need this, because every property is range-indexed by default; a specific (a, b) sort
// order across two properties together is not.
"SELECT * FROM o ORDER BY o.customerId ASC, o.orderDate DESC"

policy.CompositeIndexes.Add(new Collection<CompositePath>
{
    new() { Path = "/customerId", Order = CompositePathSortOrder.Ascending },
    new() { Path = "/orderDate", Order = CompositePathSortOrder.Descending },
});
```

### Manual vs. autoscale is a cost/predictability choice, made in code
```csharp
// Fixed RU/s: predictable bill, hard ceiling.
await database.CreateContainerIfNotExistsAsync(properties, ThroughputProperties.CreateManualThroughput(400));

// Scales between 400 (10%) and 4000 RU/s automatically based on load, at up to 1.5x the
// scaled-to rate compared to the equivalent manual value.
await database.CreateContainerIfNotExistsAsync(properties, ThroughputProperties.CreateAutoscaleThroughput(4000));
```

## Project Structure

```
CosmosIndexingAndThroughput/
├── CosmosIndexingAndThroughput/   # <- YOUR WORKSPACE. Write your code here.
│   ├── CosmosIndexingAndThroughput.csproj  #   ready to build
│   ├── Program.cs                #   replace as you work through the exercise
│   ├── appsettings.json          #   already points at the emulator's well-known endpoint/key
│   ├── Models/                   #   create as you go: Customer, Order, OrderLine
│   ├── Data/                     #   create as you go: DataSeeder, SampleOrderFactory
│   ├── Indexing/                 #   create as you go: IndexingPolicyFactory, IndexingWalkthrough
│   ├── Metrics/                  #   create as you go: CosmosQueryExtensions, RequestChargeDemo
│   ├── Throughput/               #   create as you go: ThroughputWalkthrough
│   └── Retry/                    #   create as you go: RetryPolicy, RetryWalkthrough
├── solution/                     # <- REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Models/                     Customer, Order, OrderLine
│   ├── Data/                       DataSeeder, SampleOrderFactory
│   ├── Indexing/                   IndexingPolicyFactory, IndexingWalkthrough (Parts 1-2)
│   ├── Metrics/                    CosmosQueryExtensions, RequestChargeDemo (Part 3)
│   ├── Throughput/                 ThroughputWalkthrough (Part 4)
│   └── Retry/                      RetryPolicy, RetryWalkthrough (Part 5)
├── AppHost/                      # .NET Aspire orchestration: starts the Cosmos emulator,
│   │                               provisions the database/containers, runs `solution`
│   └── Program.cs
├── tests/                        # ~20 tests against the real emulator (via Aspire.Hosting.Testing)
├── EXERCISE.md
├── GETTING_STARTED.md
└── README.md
```

## Quick Start

1. **Make sure Docker is running.** The Cosmos DB emulator is a Linux container Aspire
   starts for you -- there's nothing to `docker compose up` yourself.

2. **Navigate:**
   ```bash
   cd 11-NoSqlCosmosDb/CosmosIndexingAndThroughput
   ```

3. **Verify the build (no Docker needed for this step):**
   ```bash
   dotnet build
   ```

4. **Start learning:**
   Open [EXERCISE.md](EXERCISE.md)

## The Learning Path

### Part 0: Project Setup (10 min)
Create the folders you'll fill in.

### Part 1: Default Indexing (30 min)
See Cosmos's default indexing policy index everything automatically, including an embedded
array, and measure what that costs on every write.

### Part 2: Customizing the Indexing Policy (45 min)
Exclude the `orderLines` subtree, add a composite index, and watch a previously-failing
`ORDER BY` succeed.

### Part 3: Reading `RequestCharge` (30 min)
Instrument a point read, a single-partition query, a cross-partition query, and a write, and
compare their real RU cost side by side.

### Part 4: Manual vs. Autoscale Throughput (30 min)
Provision one container each way from the client SDK, and read back what each API actually
configured.

### Part 5: Handling `429 TooManyRequests` (35 min)
Write a manual backoff-and-retry, and understand when it's worth having on top of the SDK's
own built-in retry.

**Total Time**: 2.5-3.5 hours

## Best Practices

### Indexing
✅ Exclude subtrees you never query into directly, especially variable-length embedded
   arrays -- indexing them costs RU on every write for no corresponding read benefit
✅ Add a composite index the moment you need a multi-property `ORDER BY` -- there's no
   partial or fallback behavior, the query fails outright without one
❌ Don't assume excluding a path makes it un-queryable -- it just stops being indexed;
   Cosmos can still scan for it, at a higher cost

### Throughput
✅ Use manual throughput for steady, predictable workloads where the hard ceiling is a
   feature, not a risk
✅ Use autoscale for bursty workloads where provisioning for peak, manually, would waste
   money most of the time
❌ Don't configure either from the Aspire `AppHost` -- the hosting integration doesn't
   expose it; do it from the client SDK against the container Aspire already created

### Resilience
✅ Let the SDK's built-in retry handle occasional 429s -- that's what it's for
✅ Add your own retry/backoff on top only when you need visibility into *sustained*
   throttling (logging, alerting, fallback) that the SDK's silent retry won't give you
❌ Don't assume the emulator's behavior under load matches a real Cosmos account -- it
   accepts the throughput APIs but doesn't meaningfully enforce or scale RU/s

## Testing Considerations

The tests in `tests/` run against the **real Cosmos DB emulator**, started once for the
whole test run via `Aspire.Hosting.Testing` (the same `AppHost` project, booted in-process)
and shared across every test class through a collection fixture -- the emulator's cold start
is measured in minutes, so a fresh one per test class would make the suite impractically
slow. The retry/backoff tests are the one exception: they exercise `RetryPolicy`'s logic
directly against a constructed `CosmosException`, with no emulator involved, because a real,
reliable `429` isn't something a single test client can dependably force out of the
emulator. `dotnet test` needs Docker running, but does not need anything started by hand
first.

## Next Steps

After completing CosmosIndexingAndThroughput:

1. **Re-run Part 1's write-cost measurement mentally** against a document shape from your
   own work -- name one property or subtree in it that's indexed today but never queried.
2. **If you're working through module 11 in order, continue to CosmosConsistencyAndTransactions**
   - [../CosmosConsistencyAndTransactions](../CosmosConsistencyAndTransactions/)

## Checklist

After this project, you should be able to:

- [ ] Explain Cosmos DB's default indexing policy and its write-cost implication for large
      embedded arrays
- [ ] Exclude a subtree from indexing and add a composite index for a multi-property
      `ORDER BY`
- [ ] Read and compare `RequestCharge` across point reads, single- and cross-partition
      queries, and writes
- [ ] Provision manual and autoscale throughput from the client SDK and explain the
      cost/predictability trade-off between them
- [ ] Catch and back off from `429 TooManyRequests`, and explain when that's redundant with
      the SDK's own retry behavior and when it isn't

---

**Ready to see what your documents actually cost?** Open [EXERCISE.md](EXERCISE.md)!
