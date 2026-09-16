# Exercise: Transactions and Concurrency in Neo4j

## Overview
Module 10's `EfCoreTransactions` and module 11's `CosmosConsistencyAndTransactions`
both asked the same two questions of their database: *what happens to
concurrent writers, and what happens when a multi-step operation fails
halfway through?* This exercise asks Neo4j the same two questions, using the
same professional-network domain (`Person`, `Company`, `KNOWS`, `WORKS_AT`)
as this module's other projects.

Neo4j's answers land somewhere between the other two. Like EF Core over
Postgres, a single write is already an implicit transaction, and explicit
transactions give you full multi-statement control with `BeginTransactionAsync`
/ `CommitAsync` / `RollbackAsync`. Unlike Cosmos DB, there is no partition-key
wall to design around: a transaction can touch as many nodes and relationships
as it needs, of any labels, as long as they live in the same database. And for
concurrent writers, Neo4j reaches for old-fashioned pessimistic locking at the
node level, not the optimistic ETag/`xmin` compare-and-swap you built in
modules 10 and 11.

You'll work through five parts: implicit single-call transactions, an
explicit multi-statement "referral" transaction with a deliberate rollback,
the cross-database contrast with Cosmos DB's partition-scoped
`TransactionalBatch`, concurrent updates to the same node, and a small
unit-of-work wrapper that ties Parts 2 and 4 together.

## Prerequisites
Neo4j must be running for every part of this exercise:
```bash
cd 13-GraphDatabaseNeo4j
docker compose up -d neo4j-transactions
```
This starts a `neo4j:5-community` container named `netlearn-graphtransactions-neo4j`,
reachable at `bolt://localhost:7691` (Bolt/driver protocol) and
`http://localhost:7478` (Neo4j Browser), with credentials
`neo4j` / `graphtransactions`. Your workspace's `appsettings.json` already
points at it.

## Learning Goals
By completing this exercise, you will:
- Understand why a single `session.ExecuteWriteAsync(...)` call is already an
  implicit transaction, even when it contains several Cypher statements --
  and why that transaction is NOT scoped to one node or partition the way a
  Cosmos DB write is scoped to one partition key
- Use `session.BeginTransactionAsync()` / `tx.RunAsync(...)` / `tx.CommitAsync()`
  / `tx.RollbackAsync()` to keep a three-write business operation atomic
  across multiple nodes and relationships
- Explain, concretely, why the same three-write operation that is "just a
  transaction" in Neo4j required a denormalized partition-key workaround in
  `11-NoSqlCosmosDb/CosmosConsistencyAndTransactions`
- Observe Neo4j's default pessimistic locking: a write to a node inside a
  transaction holds a lock on it until commit or rollback, so a second
  concurrent writer waits instead of losing its update
- Factor "begin transaction, run the writes, commit or rollback" into a
  reusable `ReferralService`, mirroring `EfCoreTransactions`'s unit-of-work
  section

---

## Part 0: The Domain and Wiring

### Step 0.1: The Domain Types

**Your Task:**
Create `Domain/Person.cs`:

```csharp
namespace GraphTransactions.Domain;

public class Person
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    // Bumped every time this person successfully refers someone else --
    // see Services/ReferralService.cs. Defaults to zero for a brand-new
    // Person node.
    public int ReferralCount { get; set; }
}
```

Create `Domain/Company.cs`:

```csharp
namespace GraphTransactions.Domain;

public class Company
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
}
```

**Why does `Person` need a `ReferralCount` the other Neo4j projects in this
module don't have?** It's the mutable counter this whole exercise revolves
around -- Part 2 increments it as the third write in a multi-part
transaction, and Part 4 increments it concurrently from two writers at once.
A property nobody ever changes can't demonstrate either lesson.

### Step 0.2: An Idempotent Seeder

**Your Task:**
Create `Data/GraphSeeder.cs`:

```csharp
using Neo4j.Driver;

namespace GraphTransactions.Data;

// Idempotent: safe to call every time the app starts. The constraints use
// IF NOT EXISTS, and the seed data uses MERGE keyed on a stable business
// id, so re-running this never creates duplicate nodes or relationships.
public static class GraphSeeder
{
    public const string AliceId = "person-alice";
    public const string BobId = "person-bob";
    public const string AcmeId = "company-acme";

    public static async Task SeedAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE CONSTRAINT person_id_unique IF NOT EXISTS FOR (p:Person) REQUIRE p.id IS UNIQUE");
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE CONSTRAINT company_id_unique IF NOT EXISTS FOR (c:Company) REQUIRE c.id IS UNIQUE");
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                MERGE (alice:Person {id: $aliceId})
                ON CREATE SET alice.name = 'Alice', alice.referralCount = 0
                MERGE (bob:Person {id: $bobId})
                ON CREATE SET bob.name = 'Bob', bob.referralCount = 0
                MERGE (acme:Company {id: $acmeId})
                ON CREATE SET acme.name = 'Acme Corp'
                MERGE (alice)-[:WORKS_AT {role: 'Engineering Manager', since: date('2019-03-01')}]->(acme)
                MERGE (alice)-[:KNOWS {since: date('2018-05-12')}]->(bob)
                """,
                new { aliceId = AliceId, bobId = BobId, acmeId = AcmeId });
        });
    }
}
```

**Why three separate `ExecuteWriteAsync` calls instead of one?** They could
be one call -- each is already atomic on its own here regardless, because
`CREATE CONSTRAINT` statements in Neo4j can't run in the same transaction as
data writes. Splitting them is a Neo4j requirement, not a style choice.

**Questions to think about:**
1. Why does the seed data use `MERGE ... ON CREATE SET` instead of plain
   `CREATE`? What would happen on the *second* run of `SeedAsync` if it used
   `CREATE`?
2. Both `Person.Id` (Step 0.1) and `GraphSeeder.AliceId` (this step) default
   to a stable string, not `Domain.Person`'s `Guid.NewGuid()` default. Why
   does seed data need a *fixed* id instead of a random one?

### Step 0.3: Wire It Up

**Your Task:**
Update `Program.cs`:

```csharp
using GraphTransactions.Data;
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
Console.WriteLine("Seed data ensured (Alice, Bob, Acme Corp). Now build Part 1 below.");
```

**Run it:**
```bash
dotnet run
```
If this connects, seeds, and prints the message, you're ready for Part 1.

---

## Part 1: Implicit Transactions

### Why This Comes First
Same lesson as `EfCoreTransactions` Part 1 and `CosmosConsistencyAndTransactions`'s
single-document writes: before reaching for `BeginTransactionAsync()`, notice
that a single `session.ExecuteWriteAsync(...)` call is *already* a
transaction for every Cypher statement inside it -- with zero explicit
transaction code of your own.

### Step 1.1: Two Statements, One Implicit Transaction

**Your Task:**
Create `Demos/Part1ImplicitTransactionDemo.cs`:

```csharp
using Neo4j.Driver;

namespace GraphTransactions.Demos;

public static class Part1ImplicitTransactionDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        var personId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";

        // ONE ExecuteWriteAsync call, but TWO Cypher statements inside it --
        // both succeed or both roll back, with no BeginTransactionAsync
        // anywhere in this method.
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE (:Person {id: $personId, name: 'Implicit Demo Person', referralCount: 0})",
                new { personId });
            await tx.RunAsync(
                "CREATE (:Company {id: $companyId, name: 'Implicit Demo Co'})",
                new { companyId });
        });

        var bothExist = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                OPTIONAL MATCH (p:Person {id: $personId})
                OPTIONAL MATCH (c:Company {id: $companyId})
                RETURN p IS NOT NULL AS personExists, c IS NOT NULL AS companyExists
                """,
                new { personId, companyId });
            var record = await cursor.SingleAsync();
            return record["personExists"].As<bool>() && record["companyExists"].As<bool>();
        });
        Console.WriteLine($"Both statements committed together: {bothExist}");
    }
}
```

### Step 1.2: Prove the Rollback Half

**Your Task:**
Extend `RunAsync` to throw between two statements and confirm neither
survives:

```csharp
        var failedPersonId = $"person-{Guid.NewGuid()}";
        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    "CREATE (:Person {id: $failedPersonId, name: 'Should Not Survive', referralCount: 0})",
                    new { failedPersonId });

                throw new InvalidOperationException("Simulated failure inside the implicit transaction.");
            });
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Caught: {ex.Message}");
        }

        var survivedCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person {id: $failedPersonId}) RETURN count(p) AS count",
                new { failedPersonId });
            var record = await cursor.SingleAsync();
            return record["count"].As<int>();
        });
        Console.WriteLine($"Person from the failed implicit transaction exists: {survivedCount > 0} (expected: False)");
```

**Run it:**
```bash
dotnet run -- 1
```

**Why this matters, and the boundary it does NOT solve:** exactly like
`EfCoreTransactions` Part 1's "one `SaveChanges()`, two changes," this is
atomic for free. The limit is the same too: the moment your business
operation needs a **second** `session.ExecuteWriteAsync` call -- because
something else has to happen in between -- you're back to needing an
explicit transaction, which is exactly Part 2's problem.

**Questions to think about:**
1. `CosmosConsistencyAndTransactions`'s single-document writes are atomic
   only for one document. This implicit transaction is atomic across a
   `Person` node AND a `Company` node -- two different labels. What does
   that already tell you about how Neo4j scopes a transaction, compared to
   how Cosmos DB scopes one?
2. If the second `tx.RunAsync` call in Step 1.1 violated a uniqueness
   constraint instead of a deliberate `throw`, would the first `CREATE`
   still roll back? Why would the mechanism be the same either way?

---

## Part 2: An Explicit Multi-Statement Transaction -- The Referral Scenario

### The Problem Part 1 Doesn't Solve
A **referral**: person A refers person B to a company. This is not one
write -- it's three: B gets a `WORKS_AT` relationship to the company, A and B
get a `KNOWS` relationship (or an existing one is reused), and A's
`referralCount` goes up by one. All three touch different graph elements (a
`Company` node, B's `Person` node, A's `Person` node), and all three must
succeed together or not at all.

### Step 2.1: The Three Writes, Explicitly Transacted

**Your Task:**
Create `Demos/Part2ExplicitReferralTransactionDemo.cs`:

```csharp
using Neo4j.Driver;

namespace GraphTransactions.Demos;

public static class Part2ExplicitReferralTransactionDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        var referrerId = $"person-{Guid.NewGuid()}";
        var newHireId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE (:Person {id: $referrerId, name: 'Referrer', referralCount: 0})", new { referrerId });
            await tx.RunAsync(
                "CREATE (:Person {id: $newHireId, name: 'New Hire', referralCount: 0})", new { newHireId });
            await tx.RunAsync(
                "CREATE (:Company {id: $companyId, name: 'Referral Target Co'})", new { companyId });
        });

        Console.WriteLine("--- Successful referral: all three writes commit together ---");
        await RunReferralAsync(session, referrerId, newHireId, companyId, simulateFailure: false);
        await PrintStateAsync(session, referrerId, newHireId, companyId);
    }

    private static async Task RunReferralAsync(
        IAsyncSession session, string referrerId, string newHireId, string companyId, bool simulateFailure)
    {
        await using var tx = await session.BeginTransactionAsync();
        try
        {
            // Write 1: the new hire starts working at the company.
            await tx.RunAsync(
                """
                MATCH (p:Person {id: $newHireId}), (c:Company {id: $companyId})
                MERGE (p)-[r:WORKS_AT]->(c)
                ON CREATE SET r.role = 'Engineer', r.since = date()
                """,
                new { newHireId, companyId });

            // Write 2: the referrer and the new hire now KNOWS each other.
            await tx.RunAsync(
                """
                MATCH (a:Person {id: $referrerId}), (b:Person {id: $newHireId})
                MERGE (a)-[k:KNOWS]->(b)
                ON CREATE SET k.since = date()
                """,
                new { referrerId, newHireId });

            if (simulateFailure)
            {
                throw new InvalidOperationException(
                    "Simulated failure between the KNOWS write and the referralCount increment.");
            }

            // Write 3: credit the referrer.
            await tx.RunAsync(
                "MATCH (a:Person {id: $referrerId}) SET a.referralCount = a.referralCount + 1",
                new { referrerId });

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static async Task PrintStateAsync(
        IAsyncSession session, string referrerId, string newHireId, string companyId)
    {
        var summary = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $referrerId})
                OPTIONAL MATCH (b:Person {id: $newHireId})
                OPTIONAL MATCH (b)-[w:WORKS_AT]->(:Company {id: $companyId})
                OPTIONAL MATCH (a)-[k:KNOWS]->(b)
                RETURN a.referralCount AS referralCount, w IS NOT NULL AS worksAt, k IS NOT NULL AS knows
                """,
                new { referrerId, newHireId, companyId });
            var record = await cursor.SingleAsync();
            return (
                ReferralCount: record["referralCount"].As<int>(),
                WorksAt: record["worksAt"].As<bool>(),
                Knows: record["knows"].As<bool>());
        });

        Console.WriteLine(
            $"referrer.referralCount={summary.ReferralCount}, WORKS_AT exists={summary.WorksAt}, KNOWS exists={summary.Knows}");
    }
}
```

Notice the shape: `BeginTransactionAsync()` once, `tx.RunAsync(...)` three
times on the **same** transaction object, then one `CommitAsync()` -- or, on
any exception, `RollbackAsync()` in a `catch` before rethrowing. This is the
identical pattern `EfCoreTransactions` Part 2 uses around multiple
`SaveChanges()` calls, just with `tx.RunAsync` in place of `SaveChanges`.

### Step 2.2: Prove the Rollback Covers ALL THREE Writes, Not Just the Last

**Your Task:**
Extend `RunAsync` to call `RunReferralAsync` a second time with
`simulateFailure: true`, and confirm that neither the `WORKS_AT` write nor
the `KNOWS` write -- both of which ran and were sent to the server **before**
the throw -- survived either:

```csharp
        Console.WriteLine();
        Console.WriteLine("--- Failing referral: a deliberate throw between the 2nd and 3rd write ---");
        var secondHireId = $"person-{Guid.NewGuid()}";
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE (:Person {id: $secondHireId, name: 'Second Hire', referralCount: 0})",
                new { secondHireId });
        });

        try
        {
            await RunReferralAsync(session, referrerId, secondHireId, companyId, simulateFailure: true);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Caught: {ex.Message}");
        }
        await PrintStateAsync(session, referrerId, secondHireId, companyId);
```

**Run it:**
```bash
dotnet run -- 2
```

**What to look for:** the second `PrintStateAsync` call should show
`referralCount` unchanged from the first referral, `WORKS_AT exists=False`,
and `KNOWS exists=False` -- even though the WORKS_AT and KNOWS statements
both ran without error before the deliberate throw. That's the whole point:
`RollbackAsync()` undoes everything the transaction did, not just the
operation that never got to run.

**Questions to think about:**
1. What would you see in `PrintStateAsync`'s output if you moved the
   `simulateFailure` throw to happen **after** the referralCount write
   instead of before it? Would that still prove anything about rollback?
2. `RunReferralAsync` rethrows after rolling back. What would change for a
   caller if it swallowed the exception instead? Would the caller be able to
   tell a successful referral from a silently-failed one?

---

## Part 3: The Cross-Database Contrast with Cosmos DB

### Why This Part Has No New Code
Go back and reread `11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/EXERCISE.md`
Part 0 and Part 4, specifically the `CustomerLoyaltyProfile` design. That
project's business operation -- placing an order should also credit loyalty
points, atomically -- is structurally the *same shape* as this project's
referral: one write that should logically span more than one entity.

Cosmos DB's answer was `TransactionalBatch`, which **only** guarantees
atomicity for operations against documents that share both a container and a
partition key value. The natural model there put `Customer` (partitioned by
`/id`) and `Order` (partitioned by `/customerId`) in different partitions
with nothing in common -- so `TransactionalBatch` flatly could not batch
them. The project's fix was to invent `CustomerLoyaltyProfile`: a
denormalized copy of the loyalty data, moved **into** the `orders`
container, under the **same** `/customerId` partition as the order it needed
to be atomic with. Redesigning the data model was the price of atomicity.

### Step 3.1: See the Same Shape Solved With No Redesign

**Your Task:**
Create `Demos/Part3CrossDatabaseContrastDemo.cs`:

```csharp
namespace GraphTransactions.Demos;

public static class Part3CrossDatabaseContrastDemo
{
    public static Task RunAsync()
    {
        Console.WriteLine("Cosmos DB: TransactionalBatch required copying loyalty data INTO the");
        Console.WriteLine("orders container under the order's own partition key before an order-plus-");
        Console.WriteLine("points write could be atomic. See CustomerLoyaltyProfile in module 11.");
        Console.WriteLine();
        Console.WriteLine("Neo4j: Part 2's referral transaction touched a Company node, the new");
        Console.WriteLine("hire's Person node, AND the referrer's Person node -- three elements that,");
        Console.WriteLine("partitioned the Cosmos way (Person by /id, Company by /id), would very");
        Console.WriteLine("likely land in three different partitions. No redesign was needed here.");
        return Task.CompletedTask;
    }
}
```

Run Part 2 again and count: the referral transaction wrote to **two distinct
`Person` nodes and one `Company` node** -- three separate graph elements,
none of which needed to be co-located, denormalized, or redesigned in any
way for the transaction to be atomic. `BeginTransactionAsync` /
`CommitAsync` / `RollbackAsync` don't know or care how many nodes,
relationships, or labels a transaction touches, as long as everything lives
in the same database.

**This is the module's culminating lesson, not a footnote:** the two
databases answer "can I atomically write across more than one entity?" in
opposite ways by default. Cosmos DB's answer is "only if you design your
partition keys to make it possible." Neo4j's answer is "yes, unconditionally,
as long as you're in one transaction" -- full stop.

**Questions to think about:**
1. What does Neo4j give up to make this true? (Hint: re-read this module's
   `docker-compose.yml` comment about Neo4j Community Edition supporting
   only one database per instance, and think about what happens as a graph
   grows well past what fits on one machine.)
2. If you had to explain to a teammate choosing between Cosmos DB and Neo4j
   for a new system *purely* on this one axis -- multi-entity write
   atomicity -- what single sentence would you use? What would you leave out
   of that sentence because it's a different, separate trade-off (hint: think
   about horizontal scale)?

---

## Part 4: Concurrent Updates and Neo4j's Default Locking

### The Problem
Two sessions both try to increment the **same** `Person.referralCount` at
the same time. Modules 10 and 11 solved this with **optimistic**
concurrency: read a version marker (`xmin`, `_etag`), write back only if it
hasn't moved, and if it has, catch the failure and retry. Neo4j's default
behavior is different: a write to a node inside a transaction takes a lock
on that node, held until the transaction commits or rolls back. A second
writer that tries to touch the same node **waits** for the lock instead of
racing to overwrite -- or being rejected outright.

### Step 4.1: Two Real Concurrent Writers, One Node

**Your Task:**
Create `Demos/Part4ConcurrentIncrementDemo.cs`:

```csharp
using Neo4j.Driver;

namespace GraphTransactions.Demos;

public static class Part4ConcurrentIncrementDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        var personId = $"person-{Guid.NewGuid()}";
        await using (var setupSession = driver.AsyncSession())
        {
            await setupSession.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    "CREATE (:Person {id: $personId, name: 'Concurrent Target', referralCount: 0})",
                    new { personId });
            });
        }

        Console.WriteLine("Starting two concurrent transactions against the SAME Person node...");

        // Writer A holds its lock for 300ms after its SET before committing.
        // Writer B starts at the same instant with no delay -- via
        // Task.WhenAll below -- so whichever one loses the race for the
        // node's write lock genuinely BLOCKS and waits. This is a real
        // overlapping race, not two sequential writes dressed up to look
        // concurrent.
        var taskA = IncrementWithDelayAsync(driver, personId, "Writer A", delayBeforeCommitMs: 300);
        var taskB = IncrementWithDelayAsync(driver, personId, "Writer B", delayBeforeCommitMs: 0);

        await Task.WhenAll(taskA, taskB);

        await using var readSession = driver.AsyncSession();
        var finalCount = await readSession.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person {id: $personId}) RETURN p.referralCount AS referralCount",
                new { personId });
            var record = await cursor.SingleAsync();
            return record["referralCount"].As<int>();
        });

        Console.WriteLine($"Final referralCount: {finalCount} (expected 2 -- BOTH increments landed, no lost update).");
    }

    private static async Task IncrementWithDelayAsync(
        IDriver driver, string personId, string label, int delayBeforeCommitMs)
    {
        await using var session = driver.AsyncSession();
        await using var tx = await session.BeginTransactionAsync();
        try
        {
            // The write lock on this node is taken here, as soon as the SET
            // statement runs -- not at BeginTransactionAsync, and not at
            // CommitAsync.
            await tx.RunAsync(
                "MATCH (p:Person {id: $personId}) SET p.referralCount = p.referralCount + 1",
                new { personId });

            if (delayBeforeCommitMs > 0)
            {
                Console.WriteLine($"{label}: holding the lock for {delayBeforeCommitMs}ms before committing...");
                await Task.Delay(delayBeforeCommitMs);
            }

            await tx.CommitAsync();
            Console.WriteLine($"{label}: committed.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
```

**Run it:**
```bash
dotnet run -- 4
```

**What to look for:** whichever writer's `SET` runs second has to wait --
you'll see its "committed" line print noticeably later than the other's --
but the final count is still `2`. Nobody's increment was lost, and nobody
had to catch an exception and retry.

**Compare to modules 10/11.** There, `DbUpdateConcurrencyException` /
`412 PreconditionFailed` are *failures* your code has to catch and react to.
Here, there's no failure to catch at all in the normal case -- the second
writer's statement simply takes longer to return, because it's parked
waiting for a lock, then runs against whatever the node's current value is
once it gets the lock. This is pessimistic concurrency: block first-comers'
competitors, rather than let everyone race and reject the loser afterward.

**Questions to think about:**
1. What happens if Writer A's transaction never calls `CommitAsync()` or
   `RollbackAsync()` at all -- say, the process crashes mid-transaction?
   What does that imply about how long Writer B could theoretically wait?
2. Pessimistic locking trades throughput (writers queue up instead of
   running in parallel) for simplicity (no retry loop, no lost updates,
   ever). When would you actively prefer Cosmos/Postgres's optimistic
   approach instead, even knowing it can reject a writer outright?

---

## Part 5: The `ReferralService` Unit-of-Work Wrapper

### Why
Part 2's `RunReferralAsync` and Part 4's `IncrementWithDelayAsync` both
repeat the same shape: begin a transaction, run some writes, commit on
success, roll back on failure. `EfCoreTransactions` Part 6 hit the identical
repetition and factored it into `IUnitOfWork`/`TransferService`. Do the same
here with a single `ReferralService` that owns the referral transaction
end-to-end.

### Step 5.1: `IReferralService` and `ReferralResult`

**Your Task:**
Create `Services/ReferralResult.cs`:

```csharp
namespace GraphTransactions.Services;

public record ReferralResult(string ReferrerId, string NewHireId, string CompanyId, int ReferrerReferralCount);
```

Create `Services/IReferralService.cs`:

```csharp
namespace GraphTransactions.Services;

public interface IReferralService
{
    Task<ReferralResult> ReferAsync(
        string referrerId,
        string newHireId,
        string companyId,
        string role,
        bool simulateFailureBeforeIncrement = false);
}
```

**Why does `ReferAsync` take a `simulateFailureBeforeIncrement` flag?** It's
a seam for tests and demos to force the same failure Part 2 triggered by
hand, without needing a second, parallel "this one always breaks" code path.
A real caller never passes `true`.

### Step 5.2: `ReferralService`

**Your Task:**
Create `Services/ReferralService.cs`:

```csharp
using Neo4j.Driver;

namespace GraphTransactions.Services;

public class ReferralService : IReferralService
{
    private readonly IDriver _driver;

    public ReferralService(IDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    public async Task<ReferralResult> ReferAsync(
        string referrerId,
        string newHireId,
        string companyId,
        string role,
        bool simulateFailureBeforeIncrement = false)
    {
        await using var session = _driver.AsyncSession();
        await using var tx = await session.BeginTransactionAsync();
        try
        {
            await tx.RunAsync(
                """
                MATCH (p:Person {id: $newHireId}), (c:Company {id: $companyId})
                MERGE (p)-[r:WORKS_AT]->(c)
                ON CREATE SET r.role = $role, r.since = date()
                ON MATCH SET r.role = $role
                """,
                new { newHireId, companyId, role });

            await tx.RunAsync(
                """
                MATCH (a:Person {id: $referrerId}), (b:Person {id: $newHireId})
                MERGE (a)-[k:KNOWS]->(b)
                ON CREATE SET k.since = date()
                """,
                new { referrerId, newHireId });

            if (simulateFailureBeforeIncrement)
            {
                throw new InvalidOperationException(
                    "Simulated failure between the KNOWS write and the referralCount increment.");
            }

            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $referrerId})
                SET a.referralCount = a.referralCount + 1
                RETURN a.referralCount AS referralCount
                """,
                new { referrerId });
            var record = await cursor.SingleAsync();
            var referralCount = record["referralCount"].As<int>();

            await tx.CommitAsync();
            return new ReferralResult(referrerId, newHireId, companyId, referralCount);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
```

This is the exact three-write shape from Part 2, moved behind an interface.
Nothing about the transaction handling changed -- what changed is that a
caller no longer needs to know it exists.

### Step 5.3: Use It Through DI

**Your Task:**
Add to `Program.cs`:

```csharp
using GraphTransactions.Services;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton(driver);
services.AddSingleton<IReferralService, ReferralService>();
await using var provider = services.BuildServiceProvider();

var referralService = provider.GetRequiredService<IReferralService>();

var newHireId = $"person-{Guid.NewGuid()}";
var result = await referralService.ReferAsync(GraphSeeder.AliceId, newHireId, GraphSeeder.AcmeId, "Product Designer");
Console.WriteLine($"Referral succeeded via ReferralService. Alice's referralCount is now {result.ReferrerReferralCount}.");

try
{
    await referralService.ReferAsync(
        GraphSeeder.AliceId, $"person-{Guid.NewGuid()}", GraphSeeder.AcmeId, "Analyst",
        simulateFailureBeforeIncrement: true);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Caught (expected): {ex.Message}");
    Console.WriteLine("The wrapper rolled back all three writes -- callers never see a partial referral.");
}
```

**Run it:**
```bash
dotnet run -- 5
```

**Questions to think about:**
1. `ReferralService` has no `try`/`catch`/rollback visible to its caller --
   `Program.cs`'s second call just gets an exception and a fully rolled-back
   database. What did factoring this out buy you, compared to Part 2's
   `RunReferralAsync` being called directly?
2. `EfCoreTransactions`'s `IUnitOfWork` is a generic wrapper any operation can
   use (`ExecuteAsync<T>(Func<Task<T>> operation, ...)`). `ReferralService`
   here is specific to one operation. What would a generic
   `IGraphUnitOfWork` look like for this project, and what would you gain or
   lose by building one instead of a purpose-built service per operation?

---

## Checking Your Work
A complete reference implementation lives in `solution/`, organized as one
file per part under `solution/Demos/` plus `solution/Services/ReferralService.cs`,
and `tests/` proves the behavior against a real, disposable Neo4j container
(via Testcontainers).

```bash
dotnet run --project solution -- 1     # or 2, 3, 4, 5, or "all"
dotnet test tests                      # needs Docker running (starts the container itself)
```

Build your own version of each part first. When you compare, notice in
particular that `solution/Services/ReferralService.cs` and Part 2's
`RunReferralAsync` are structurally identical -- Part 5 doesn't introduce new
transaction-handling ideas, it just relocates code you already wrote.

---

## Reflection Questions

1. **Why is a single `ExecuteWriteAsync()` call already an implicit
   transaction, and what specifically forces you to reach for
   `BeginTransactionAsync()` instead?** Compare your answer to
   `EfCoreTransactions`'s equivalent question about `SaveChanges()`.

2. **The referral transaction in Part 2 touches three different graph
   elements with no partition-key planning at all.** What did
   `CosmosConsistencyAndTransactions` have to build (`CustomerLoyaltyProfile`)
   to make a structurally similar operation atomic? Why didn't Neo4j need an
   equivalent?

3. **Neo4j's default concurrency control is pessimistic (locks, blocking);
   modules 10 and 11's is optimistic (version markers, reject-and-retry).**
   Under heavy contention on the same node, which approach degrades more
   gracefully, and which fails more visibly? Is "fails more visibly"
   necessarily worse?

4. **`ReferralService.ReferAsync`'s `simulateFailureBeforeIncrement` parameter
   only exists for tests and demos.** Real production code doesn't usually
   ship a "please fail here" flag on a public method. What's a better way to
   inject a failure partway through a transaction for testing purposes,
   without polluting the production API surface?

5. **You've now seen three different atomicity/concurrency stories across
   modules 10, 11, and 13: row-level MVCC + `xmin`, HTTP ETags +
   partition-scoped batches, and node-level locks + unrestricted
   multi-entity transactions.** If a system needed both graph traversal
   queries AND strict multi-entity transactional writes at high concurrency,
   what would you actually want to know about Neo4j's clustering story
   before picking it, that this exercise's single-instance setup can't tell
   you?

---

## Summary

You've learned:
- Why a single `ExecuteWriteAsync()` call is already an implicit transaction
  for every statement inside it
- Explicit transactions with `BeginTransactionAsync()` / `CommitAsync()` /
  `RollbackAsync()` across a three-write referral operation, and proof that
  rollback undoes ALL of a transaction's writes, not just the last one
- The concrete contrast with Cosmos DB's partition-scoped `TransactionalBatch`
  -- the same multi-entity write that required a denormalized workaround in
  module 11 needed no redesign at all here
- Neo4j's default pessimistic, node-level locking for concurrent writers, and
  how it differs from modules 10/11's optimistic concurrency
- A `ReferralService` unit-of-work wrapper, mirroring `EfCoreTransactions`'s
  `IUnitOfWork`/`TransferService` pattern

## Next Steps
Compare this project against
[10-EntityFrameworkCore/EfCoreTransactions](../../10-EntityFrameworkCore/EfCoreTransactions/)
and [11-NoSqlCosmosDb/CosmosConsistencyAndTransactions](../../11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/)
side by side -- three databases, the same two questions about atomicity and
concurrency, three genuinely different answers. Then look at this module's
other Neo4j projects to see how the same driver handles modeling, querying,
traversals, and algorithms.

---

**Happy Learning!**
