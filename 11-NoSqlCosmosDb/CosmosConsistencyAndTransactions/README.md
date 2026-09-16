# CosmosConsistencyAndTransactions - Consistency and Transactions in Azure Cosmos DB

## Overview
This project answers module 10's transactions-and-concurrency lesson
(`EfCoreTransactions`) for Cosmos DB instead of Postgres. Same two
questions -- "what can concurrent writers do to each other?" and "what
happens when a multi-step write fails halfway through?" -- but Cosmos
answers both completely differently: a tunable, per-request consistency
model instead of fixed transaction isolation levels, HTTP ETags instead of
a database-internal version column, and `TransactionalBatch` instead of
`BeginTransactionAsync()` -- atomic, but only within a single partition
key, never across containers or partitions.

## What You'll Learn

### Core Concepts
- **The five consistency levels** (Strong, Bounded Staleness, Session,
  Consistent Prefix, Eventual) -- what each guarantees and where Cosmos's
  Session default sits among them
- **Client-level and per-request consistency overrides**, and the one
  rule governing both: you can only ask for something weaker than the
  account's configured default, never stronger
- **Session tokens** -- what "read your own writes" requires the moment
  an app has more than one `CosmosClient` instance
- **ETag-based optimistic concurrency** -- Cosmos's version of module 10's
  `xmin`-based compare-and-swap, via `IfMatchEtag` and HTTP `412
  Precondition Failed`
- **`TransactionalBatch`** -- atomic multi-item writes scoped to one
  partition key, and why that scoping is a hard architectural line, not a
  configurable option
- **A deliberate partition-key redesign** as the real-world answer to
  "I need atomicity across two document types" (see `Domain/CustomerLoyaltyProfile.cs`)

### Practical Skills
- Reading a Cosmos account's configured default consistency level and
  setting a client's own level
- Propagating a session token between `CosmosClient` instances
- Catching a `CosmosException` with `StatusCode == HttpStatusCode.PreconditionFailed`
  and implementing reload-and-retry
- Building and executing a `TransactionalBatch`, checking
  `IsSuccessStatusCode`, and inspecting per-operation results
- Recognizing when an operation needs cross-partition atomicity Cosmos
  cannot give you, and choosing among the real workarounds (redesign the
  partition key, a saga with compensating actions, or eventual consistency
  reconciled by a change feed)

## Project Structure

```
CosmosConsistencyAndTransactions/
├── CosmosConsistencyAndTransactions/   # Workspace -- you build this, following EXERCISE.md
│   ├── CosmosConsistencyAndTransactions.csproj
│   ├── Program.cs
│   └── appsettings.json
├── solution/                           # Complete reference implementation
│   ├── CosmosConsistencyAndTransactions.Solution.csproj
│   ├── Program.cs
│   ├── CosmosOptions.cs
│   ├── CosmosBootstrapper.cs
│   ├── Domain/
│   │   ├── Customer.cs
│   │   ├── Order.cs
│   │   ├── OrderLine.cs
│   │   ├── OrderStatus.cs
│   │   └── CustomerLoyaltyProfile.cs
│   └── Demos/
│       ├── ConsistencyLevelDemo.cs         # Part 1
│       ├── SessionTokenDemo.cs             # Part 2
│       ├── OptimisticConcurrencyDemo.cs    # Part 3
│       ├── TransactionalBatchDemo.cs       # Part 4
│       └── CrossPartitionLimitationsDemo.cs # Part 5
├── AppHost/                            # .NET Aspire orchestration (starts the emulator)
│   ├── CosmosConsistencyAndTransactions.AppHost.csproj
│   └── Program.cs
├── tests/                              # xUnit tests against the real emulator
│   ├── CosmosConsistencyAndTransactions.Tests.csproj
│   ├── CosmosFixture.cs
│   ├── ConsistencyLevelTests.cs
│   ├── SessionTokenTests.cs
│   ├── OptimisticConcurrencyTests.cs
│   └── TransactionalBatchTests.cs
├── EXERCISE.md                         # Step-by-step exercise guide
├── GETTING_STARTED.md                  # Quick start instructions
└── README.md                           # This file
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK (pinned in `global.json`)
- **Docker**, to run the Cosmos DB emulator (via Aspire, or by hand)

### Quick Start

1. **Start the emulator, one of two ways:**
   ```bash
   # Via Aspire (recommended -- also launches solution/ wired to it):
   dotnet run --project 11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/AppHost

   # Or by hand:
   docker run -p 8081:8081 -p 10250-10255:10250-10255 --name cosmos-emulator \
     mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
   ```

2. **Navigate to the workspace:**
   ```bash
   cd 11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/CosmosConsistencyAndTransactions
   ```

3. **Verify setup:**
   ```bash
   dotnet build
   ```

4. **Read the getting started guide:**
   Open [GETTING_STARTED.md](GETTING_STARTED.md)

5. **Start the exercise:**
   Open [EXERCISE.md](EXERCISE.md) and follow Part 0.

### The Learning Path

1. **Part 0: Domain and Wiring** -- the `Customer`, `Order`, and
   `CustomerLoyaltyProfile` documents, and why the third one exists
2. **Part 1: The Five Consistency Levels** -- what each guarantees, and
   the weaken-only override rule
3. **Part 2: Per-Request Overrides and Session Tokens** -- read-your-own-
   writes across multiple `CosmosClient` instances
4. **Part 3: ETag Optimistic Concurrency** -- `IfMatchEtag`, `412`, and
   reload-and-retry
5. **Part 4: `TransactionalBatch`** -- atomic multi-item writes in one
   partition, success and failure
6. **Part 5: When This Breaks** -- no cross-partition transactions, and
   the real workarounds

## An Honest Limitation
The Cosmos DB **emulator is a single node** with no cross-region
replicas. It cannot exhibit staleness, so it cannot *prove* that Bounded
Staleness, Consistent Prefix, or Eventual consistency behave differently
from Strong/Session in production -- every level will read back the
latest write on the emulator, regardless of which one you asked for. This
project is honest about that rather than pretending otherwise: what it
verifies hands-on is everything that IS real regardless of topology --
setting each level correctly via the API, the weaken-only override rule,
session token propagation, and the two genuinely-testable atomicity
mechanisms (ETags and `TransactionalBatch`).

## Testing Your Understanding
After completing the exercise, try to answer:
1. What does Session consistency actually guarantee, and what do you have
   to do yourself to keep that guarantee once your app runs as more than
   one process?
2. Why does a `412 Precondition Failed` mean "retry," not "fail the whole
   operation"?
3. Why can't `TransactionalBatch` help when the documents you need
   atomicity across live under different partition keys, even in the same
   container?
4. If you needed an atomic operation across two different customers'
   partitions, what would you actually do?

## Next Steps
This is currently the last project in this module. Compare it against
[10-EntityFrameworkCore/EfCoreTransactions](../../10-EntityFrameworkCore/EfCoreTransactions/)
-- same problem (concurrent writes, atomicity), two very different
answers.

## Additional Resources
- [Consistency levels in Azure Cosmos DB](https://learn.microsoft.com/azure/cosmos-db/consistency-levels)
- [Optimistic concurrency control](https://learn.microsoft.com/azure/cosmos-db/nosql/database-transactions-optimistic-concurrency)
- [Transactional batch operations](https://learn.microsoft.com/azure/cosmos-db/nosql/transactional-batch)

---

**Ready to begin?** Open [GETTING_STARTED.md](GETTING_STARTED.md) to start your journey!
