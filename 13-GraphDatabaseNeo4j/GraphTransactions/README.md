# GraphTransactions - Transactions and Concurrency in Neo4j

## Overview
This project teaches transactions and concurrency control in Neo4j, using
the same professional-network domain (`Person`, `Company`, `KNOWS`,
`WORKS_AT`) as the rest of this module. You'll build a "referral" scenario
that needs an atomic, multi-part write, prove Neo4j's rollback undoes every
statement in a failed transaction (not just the last one), and see Neo4j's
default pessimistic locking handle two concurrent writers touching the same
node.

It's designed to be read side by side with
[10-EntityFrameworkCore/EfCoreTransactions](../../10-EntityFrameworkCore/EfCoreTransactions/)
and [11-NoSqlCosmosDb/CosmosConsistencyAndTransactions](../../11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/) --
three databases, the same two underlying questions ("what happens to
concurrent writers?" and "what happens when a multi-step write fails
halfway?"), three different answers.

## What You'll Learn

### Core Concepts
- **Implicit vs. Explicit Transactions**: why `session.ExecuteWriteAsync(...)`
  is already atomic, and when you need `BeginTransactionAsync()` instead
- **Multi-Entity Atomicity**: a single Neo4j transaction can span any number
  of nodes and relationships, of any labels, with no partition-key planning
- **The Cosmos DB Contrast**: why the identical business operation needed a
  denormalized workaround (`TransactionalBatch`, single-partition scope) in
  module 11, and needs none here
- **Pessimistic Concurrency**: Neo4j's default node-level write locks, and
  how they differ from the optimistic ETag/`xmin` compare-and-swap used in
  modules 10 and 11
- **Unit of Work**: factoring "begin transaction, write, commit or rollback"
  into a reusable `ReferralService`

### Practical Skills
- Writing multi-statement Cypher transactions with the official
  `Neo4j.Driver`
- Proving a transaction rollback undid *every* write, not just the failing
  one, by querying for all of them afterward
- Writing a genuine concurrency test with two overlapping `Task`s, not two
  sequential calls dressed up as concurrent
- Testing against a real, disposable Neo4j instance with Testcontainers

## Project Structure

```
GraphTransactions/
├── GraphTransactions/              # Workspace -- build this from EXERCISE.md
│   ├── GraphTransactions.csproj
│   ├── Program.cs
│   └── appsettings.json
├── solution/                       # Reference implementation
│   ├── GraphTransactions.Solution.csproj
│   ├── Program.cs                  # Dispatches to each part: dotnet run -- 1|2|3|4|5|all
│   ├── appsettings.json
│   ├── Domain/
│   │   ├── Person.cs
│   │   └── Company.cs
│   ├── Data/
│   │   └── GraphSeeder.cs          # Idempotent seed: Alice, Bob, Acme Corp
│   ├── Services/
│   │   ├── IReferralService.cs
│   │   ├── ReferralService.cs      # The unit-of-work wrapper (Part 5)
│   │   └── ReferralResult.cs
│   └── Demos/
│       ├── Part1ImplicitTransactionDemo.cs
│       ├── Part2ExplicitReferralTransactionDemo.cs
│       ├── Part3CrossDatabaseContrastDemo.cs
│       └── Part4ConcurrentIncrementDemo.cs
├── tests/                           # xUnit + Testcontainers.Neo4j
│   ├── GraphTransactions.Tests.csproj
│   ├── Neo4jFixture.cs              # Shared container/driver for the whole suite
│   ├── GraphTestHelpers.cs
│   ├── GraphSeederTests.cs
│   ├── ImplicitTransactionTests.cs
│   ├── ReferralServiceTests.cs
│   └── ConcurrencyTests.cs
├── EXERCISE.md                      # Step-by-step exercise guide
├── GETTING_STARTED.md               # Quick start instructions
└── README.md                        # This file
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK (already installed)
- Docker (for the Neo4j container and for `dotnet test`)
- Basic familiarity with this module's other Neo4j projects (Cypher syntax,
  `Neo4j.Driver` basics)

### Quick Start

1. **Start Neo4j:**
   ```bash
   cd 13-GraphDatabaseNeo4j
   docker compose up -d neo4j-transactions
   ```
   This starts a container reachable at `bolt://localhost:7691` with
   credentials `neo4j` / `graphtransactions`.

2. **Navigate to the workspace project:**
   ```bash
   cd GraphTransactions/GraphTransactions
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

**Follow this sequence:**

1. **Part 0: Domain and Wiring** (15 minutes)
   - `Person`/`Company` domain types, the idempotent `GraphSeeder`
   - Connect and confirm the seed data exists

2. **Part 1: Implicit Transactions** (20 minutes)
   - See that `ExecuteWriteAsync` is already atomic for everything inside it
   - Prove a throw inside it rolls back every prior statement too

3. **Part 2: Explicit Multi-Statement Transactions** (30 minutes)
   - Build the three-write referral scenario with
     `BeginTransactionAsync`/`CommitAsync`/`RollbackAsync`
   - Simulate a mid-transaction failure and prove ALL three writes roll back

4. **Part 3: The Cross-Database Contrast** (15 minutes)
   - No new code -- read the comparison against
     `CosmosConsistencyAndTransactions`'s `TransactionalBatch` limitation

5. **Part 4: Concurrent Updates and Locking** (25 minutes)
   - Two real concurrent `Task`s incrementing the same node
   - Observe pessimistic locking: the second writer waits, doesn't lose data

6. **Part 5: The `ReferralService` Wrapper** (20 minutes)
   - Factor Part 2's transaction handling into a reusable, DI-friendly
     service

**Total Time**: ~2 hours

## Key Takeaways

After completing this project, you should be able to:

- Explain when a Neo4j write needs `BeginTransactionAsync()` and when
  `ExecuteWriteAsync`'s implicit transaction already covers it
- Write a multi-statement, multi-entity Cypher transaction and prove its
  rollback is all-or-nothing
- Explain concretely why the same multi-entity write pattern needed a
  data-model workaround in Cosmos DB but not in Neo4j
- Describe Neo4j's default node-level locking and contrast it with
  optimistic concurrency (ETags, `xmin`)
- Recognize the unit-of-work pattern and apply it to a graph database

## Testing Your Understanding

After completing the exercise, try to answer:

1. What's the smallest change to `ReferralService.ReferAsync` that would
   make it FAIL instead of silently doing nothing when the company doesn't
   exist?
2. Why does the concurrency test in `tests/ConcurrencyTests.cs` add an
   artificial delay to only one of the two writers?
3. If you needed a fourth write inside the referral transaction, what would
   you change? What would you NOT need to change?

## Common Mistakes to Avoid

- Calling `session.ExecuteWriteAsync` multiple times for one business
  operation and assuming it's still all atomic (it isn't -- each call is its
  own transaction)
- Returning an `IResultCursor` out of an `ExecuteWriteAsync`/`ExecuteReadAsync`
  delegate instead of consuming it inside the delegate
- Writing a "concurrency test" where the two writers actually run one after
  the other -- always start both `Task`s before awaiting either
- Forgetting that Cypher's `MATCH` silently matches zero rows instead of
  throwing -- a typo'd id can make part of a transaction quietly do nothing

## Next Steps

After completing GraphTransactions, compare it against:

1. **[EfCoreTransactions](../../10-EntityFrameworkCore/EfCoreTransactions/)** --
   the same two questions (concurrent writers, atomic multi-step operations)
   answered with EF Core + Postgres row-level MVCC
2. **[CosmosConsistencyAndTransactions](../../11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/)** --
   the same questions answered with Cosmos DB's tunable consistency and
   partition-scoped `TransactionalBatch`
3. This module's other Neo4j projects (`GraphModeling`, `GraphQuerying`,
   `GraphTraversals`, `GraphAlgorithms`) for the rest of the driver's surface
   area

## Additional Resources

- [Neo4j Driver Manual (.NET)](https://neo4j.com/docs/dotnet-manual/current/)
- [Neo4j Transactions Documentation](https://neo4j.com/docs/dotnet-manual/current/transactions/)
- [Neo4j Concurrency and Locking](https://neo4j.com/docs/operations-manual/current/database-internals/concurrent-transactions/)

## Tips for Success

1. **Type the code yourself** - Don't copy-paste. Muscle memory helps
   learning.
2. **Run the demos, don't just read them** - `dotnet run -- 4`'s console
   output makes the lock-waiting behavior visible in a way reading the code
   doesn't.
3. **Compare against module 10 and 11 side by side** - the contrast is the
   whole point of Part 3.
4. **Ask questions** - If something doesn't make sense, investigate or ask
   for help.

---

**Ready to begin?** Open [GETTING_STARTED.md](GETTING_STARTED.md) to start
your journey!
