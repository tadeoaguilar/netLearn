# CosmosChangeFeed - Reacting to Data Changes in Cosmos DB

## Overview
This project teaches you the Cosmos DB **change feed** through hands-on exercises. You'll build a change feed processor that watches an `orders` container and keeps a materialized `customerordersummary` read model up to date -- the closest NoSQL analog to a relational trigger, but asynchronous, at-least-once, and without deletes.

## What You'll Learn

### Core Concepts
- **The change feed**: an ordered-per-partition log of inserts and updates (not deletes)
- **The change feed processor**: the SDK's built-in consumer, with automatic partition-splitting across instances
- **Lease containers**: how progress is checkpointed so processing resumes after a restart
- **Materialized read models**: keeping an aggregate fresh without a live aggregation query
- **Idempotency under at-least-once delivery**: why recompute-from-source beats incrementing counters
- **Failure handling**: what happens when your change handler throws, and where retry/dead-letter logic belongs

### Practical Skills
- Wiring up `GetChangeFeedProcessorBuilder` with a lease container
- Writing an idempotent change handler
- Polling for eventually-consistent results instead of assuming a fixed delay is enough
- Testing change feed behavior against a real Cosmos DB emulator via .NET Aspire

## Project Structure

```
CosmosChangeFeed/
├── CosmosChangeFeed/                    # Your workspace -- start here
│   ├── CosmosChangeFeed.csproj
│   ├── Program.cs                       # Follow EXERCISE.md to build this out
│   └── appsettings.json                 # Emulator connection string
├── solution/                            # Reference solution (peek if you get stuck)
│   ├── CosmosChangeFeed.Solution.csproj
│   ├── Models.cs                        # Order, OrderLine, Customer, CustomerOrderSummary
│   ├── OrderChangeHandler.cs            # The idempotent, recompute-based change handler
│   ├── Program.cs                       # Processor setup, seeding, polling
│   └── appsettings.json
├── AppHost/                             # .NET Aspire orchestration (starts the emulator for you)
│   ├── CosmosChangeFeed.AppHost.csproj
│   └── Program.cs
├── tests/                               # Integration + handler tests against the real emulator
│   ├── CosmosChangeFeed.Tests.csproj
│   ├── CosmosEmulatorFixture.cs         # Shared emulator/AppHost lifecycle (starts once per run)
│   ├── SmokeTests.cs
│   ├── ChangeFeedProcessorTests.cs      # End-to-end: real processor, real containers
│   └── OrderChangeHandlerTests.cs       # Handler called directly, idempotency-focused
├── EXERCISE.md                          # Step-by-step exercise guide
├── GETTING_STARTED.md                   # Quick start instructions
└── README.md                            # This file
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK (already installed)
- Docker Desktop (or another Docker-compatible daemon) running -- required to start the Cosmos DB emulator via Aspire
- Your favorite code editor (VS Code, Visual Studio, Rider)
- Basic C# and Cosmos DB knowledge (if you haven't done `CosmosModeling` yet in this module, do that one first)

### Quick Start

1. **Navigate to the project:**
   ```bash
   cd 11-NoSqlCosmosDb/CosmosChangeFeed/CosmosChangeFeed
   ```

2. **Verify setup:**
   ```bash
   dotnet build
   ```

3. **Read the getting started guide:**
   Open [GETTING_STARTED.md](GETTING_STARTED.md)

4. **Start the exercise:**
   Open [EXERCISE.md](EXERCISE.md) and follow Part 1

### The Learning Path

**Follow this sequence:**

1. **Part 1: What the Change Feed Is (and Isn't)** (15 minutes)
   - Ordered-per-partition log, not a transactional trigger
   - No deletes, unless soft-deleted

2. **Part 2: Setting Up the Processor** (30 minutes)
   - Domain models
   - `GetChangeFeedProcessorBuilder`, lease containers, instance names

3. **Part 3: Building the Materialized Read Model** (30 minutes)
   - The recompute-based `OrderChangeHandler`
   - Why same-partition-key design makes the recompute cheap

4. **Part 4: At-Least-Once Delivery and Idempotency** (25 minutes)
   - Why recompute-from-source is idempotent
   - Why naive increments are not, and what fixing that would cost

5. **Part 5: Handling Processor Failures** (15 minutes, no code)
   - Default behavior on a thrown exception
   - Where retry/dead-letter logic belongs

6. **Part 6: Put It Together** (20 minutes)
   - Wire up the full demo: write orders, poll for the summary, print it

**Total Time**: ~2-2.5 hours -- longer than the module's other projects, because change feed results take real wall-clock time to appear (lease acquisition, poll intervals, and the handler's own query all add latency you can't shortcut).

## Key Takeaways

After completing this project, you should be able to:

- Explain what the change feed is, and its real limitations (no deletes, at-least-once, eventual)
- Set up a change feed processor with a lease container and understand what each part does
- Build a materialized read model updated asynchronously by a background processor
- Design a change handler that's safe under at-least-once redelivery
- Know where retry and dead-letter logic belong when a handler can fail

## Examples Covered

### The Idempotent Handler (Recompute)
```csharp
private async Task RecomputeSummaryAsync(string customerId, CancellationToken ct)
{
    // Recomputes TotalOrders/TotalSpent/LastOrderDate from every Order
    // in this customer's partition -- not from a running counter.
    // Redelivered the same change? Same result, every time.
    ...
    await _summaryContainer.UpsertItemAsync(summary, new PartitionKey(customerId), cancellationToken: ct);
}
```
**Why:** the result depends only on current source data, never on how many times the handler has already run.

### The Non-Idempotent Trap (Increment)
```csharp
summary.TotalOrders++;                      // redelivered? counted twice.
summary.TotalSpent += order.TotalAmount;    // redelivered? added twice.
```
**Problem:** silently wrong totals on redelivery, with no exception to alert you.

### Polling for an Eventually-Consistent Result
```csharp
// NOT: await Task.Delay(1000); var summary = await ReadAsync(...);
// INSTEAD:
while (!cts.IsCancellationRequested)
{
    var summary = await TryReadSummaryAsync(customerId);
    if (summary is { TotalOrders: >= 2 }) return summary;
    await Task.Delay(250, cts.Token);
}
```
**Why:** processor startup, lease acquisition, and poll intervals mean there's no fixed delay that's both fast and reliable.

## Testing Your Understanding

After completing the exercises, try to answer:

1. What's the difference between "the change feed" and "a relational trigger," beyond timing?
2. Why can't the change feed see hard deletes?
3. Why does recomputing from source data give you idempotency almost for free?
4. What two problems does the lease container solve?
5. If your handler throws on every batch, what happens to the rest of the container's partitions?

## Common Mistakes to Avoid

- Assuming a write to `orders` is immediately reflected in `customerordersummary` -- it's asynchronous, always poll with a timeout
- Incrementing counters in the change handler instead of recomputing (breaks under redelivery)
- Reusing a processor name across genuinely different consumers that should have independent lease state
- Assuming the change feed will tell you about deletes -- it won't, unless you soft-delete
- Letting a handler exception silently stall a partition forever with no dead-letter path

## Next Steps

This project is part of module `11-NoSqlCosmosDb`, alongside projects covering Cosmos DB data modeling, partitioning strategy, and query patterns. If you haven't already, `CosmosModeling` is a good project to do first -- it covers the base `CosmosClient`/container setup this project builds on.

## Additional Resources

- [Change feed in Azure Cosmos DB](https://learn.microsoft.com/en-us/azure/cosmos-db/change-feed)
- [Change feed processor](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/change-feed-processor)
- [Change feed design patterns](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/change-feed-design-patterns)

## Tips for Success

1. **Type the code yourself** - Don't copy-paste. Muscle memory helps learning.
2. **Poll, don't guess** - resist the urge to replace a poll loop with a fixed delay "just to make the test pass faster."
3. **Run frequently** - run the demo after each part to see the processor's behavior for yourself.
4. **Think about real scenarios** - what other read models would benefit from this pattern in a real system?
5. **Ask questions** - if the eventual-consistency timing feels confusing, that's the point of Part 1 -- investigate or ask for help.

---

**Ready to begin?** Open [GETTING_STARTED.md](GETTING_STARTED.md) to start your journey!
