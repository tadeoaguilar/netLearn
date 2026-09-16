# EfCoreTransactions - Transactions and Concurrency with EF Core + PostgreSQL

## Overview
Every other module in this repository has been calling `SaveChanges()` and
trusting it to do the right thing. This project is about what "the right
thing" actually means: when EF Core opens a transaction for you, when it
doesn't, how to take over explicitly, how Postgres detects that two writers
stepped on each other, and how to recover instead of just crashing.

The domain is the classic bank-transfer example -- `Account` and `Transfer`
-- because "what happens if this fails halfway through?" has a concrete,
checkable answer: a balance that is or isn't $50 too high.

## What You'll Learn

### Transaction Control
- **Implicit transactions**: why a single `SaveChanges()` call is already
  atomic for everything it's tracking
- **Explicit transactions**: `BeginTransactionAsync()` / `CommitAsync()` /
  `RollbackAsync()`, once an operation spans more than one `SaveChanges()`
- **Savepoints**: undoing one step of a transaction without aborting the
  whole thing -- and why Postgres requires this after any failed statement

### Concurrency
- **Optimistic concurrency**: mapping Postgres's `xmin` system column as an
  EF Core concurrency token, catching `DbUpdateConcurrencyException`, and
  reload-and-retry
- **Isolation levels**: Read Committed (the default) vs. Serializable, and
  retrying a genuine `40001` serialization failure

### Structure
- **Unit of work**: factoring "begin, do the work, commit or rollback" out
  of every individual operation

## Why This Matters

### Real-World Scenarios

**Implicit transactions:**
```csharp
// Already atomic -- no transaction code needed.
alice.Balance -= 50m;
bob.Balance += 50m;
await context.SaveChangesAsync();
```

**Explicit transactions:**
```csharp
// Two SaveChanges() calls now need to succeed or fail TOGETHER.
await using var transaction = await context.Database.BeginTransactionAsync();
alice.Balance -= amount;
await context.SaveChangesAsync();
// ... anything could go wrong here ...
bob.Balance += amount;
await context.SaveChangesAsync();
await transaction.CommitAsync();
```

**Optimistic concurrency:**
```csharp
try
{
    await context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException)
{
    await context.Entry(account).ReloadAsync();
    // reapply intent on the fresh row, try again
}
```

## Project Structure

```
EfCoreTransactions/
├── EfCoreTransactions/          # <- YOUR WORKSPACE. Write your code here.
│   ├── EfCoreTransactions.csproj
│   ├── Program.cs               #   replace as you work through EXERCISE.md
│   └── appsettings.json         #   already points at efcore_transactions
│
├── solution/                    # <- REFERENCE IMPLEMENTATION.
│   ├── Domain/                  #   Account, Transfer, TransferStatus
│   ├── Data/                    #   BankDbContext, configurations, SnakeCaseNaming
│   ├── Services/                #   UnitOfWork, TransferService, retry policy
│   ├── Demos/                   #   one runnable demo per part
│   └── Program.cs
│
├── tests/                       # <- proves the behaviour against real Postgres
│
├── EXERCISE.md                  # The work, in 6 parts (+ setup)
├── GETTING_STARTED.md           # This file's companion
└── README.md
```

## Quick Start

1. **Start Postgres for the module** (once, from `10-EntityFrameworkCore/`):
   ```bash
   cd 10-EntityFrameworkCore
   docker compose up -d
   ```

2. **Navigate:**
   ```bash
   cd EfCoreTransactions/EfCoreTransactions
   ```

3. **Verify:**
   ```bash
   dotnet build
   ```

4. **Start learning:**
   Open [EXERCISE.md](EXERCISE.md)

## The Learning Path

### Part 0: Domain and DbContext (20 min)
`Account`, `Transfer`, snake_case naming, the `xmin` concurrency mapping, a
Postgres CHECK constraint.

### Part 1: Implicit Transactions (15 min)
One `SaveChanges()` call, two tracked changes, already atomic.

### Part 2: Explicit Transactions (35 min)
`BeginTransactionAsync()` / `CommitAsync()` / `RollbackAsync()` across two
`SaveChanges()` calls, with a simulated mid-operation failure.

**Use Case**: any business operation -- not just money transfers -- that
needs more than one write to the database to complete.

### Part 3: Savepoints (25 min)
`CreateSavepointAsync()` / `RollbackToSavepointAsync()` to undo one step
without aborting the whole transaction.

**Use Case**: a multi-step batch job where one step's failure shouldn't
throw away everything before it.

### Part 4: Optimistic Concurrency (30 min)
Two `DbContext` instances race to update the same row; `xmin` catches it.

**Use Case**: any row two users might edit around the same time.

### Part 5: Isolation Levels (30 min)
Read Committed vs. Serializable, and a real `40001` retry loop.

**Use Case**: financial or inventory logic where "read, decide, write" must
not silently act on stale data.

### Part 6: Unit of Work (25 min)
`ITransferService` / `UnitOfWork`, tying Parts 2-5 together behind one call.

**Total Time**: 3-3.5 hours

## Concept Deep Dive

### 1. Implicit vs. Explicit Transactions

**The default**: EF Core wraps every `SaveChanges()` call in its own
transaction automatically. That's enough as long as one call is the entire
operation.

**The moment it isn't enough**: as soon as an operation needs to read back
what it just wrote, call out to something else, or otherwise can't fit in
one `SaveChanges()`, you need an explicit transaction around all of it.

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();
try
{
    // ... multiple SaveChanges() calls ...
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

**When to Use**:
- More than one `SaveChanges()` call per business operation
- Anything that must be all-or-nothing across multiple statements

---

### 2. Savepoints

**Problem**: one statement inside a transaction fails; Postgres marks the
*whole* transaction aborted, refusing every further command.

**Solution**: a savepoint you can roll back to individually.

```csharp
await transaction.CreateSavepointAsync("risky-step");
try
{
    // ... work that might fail a constraint ...
}
catch (DbUpdateException)
{
    await transaction.RollbackToSavepointAsync("risky-step");
    // transaction is usable again; earlier work is untouched
}
```

**When to Use**:
- Multi-step operations where one step's failure is expected/recoverable
- Batch processing where you want partial progress, not all-or-nothing

---

### 3. Optimistic Concurrency (`xmin`)

**Problem**: two requests load the same row; the second save silently
overwrites the first (lost update).

**Solution**: map Postgres's `xmin` system column as a concurrency token.

```csharp
builder.Property(a => a.Version)
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .ValueGeneratedOnAddOrUpdate()
    .IsConcurrencyToken();
```

EF Core folds it into every `UPDATE`'s `WHERE` clause. A stale write matches
zero rows and throws `DbUpdateConcurrencyException` instead of silently
succeeding.

**When to Use**:
- Any row more than one process might update concurrently
- Whenever "last write wins" is the wrong answer

---

### 4. Isolation Levels

**Read Committed** (default): sees other transactions' committed changes
immediately, even mid-transaction. Cheap, but "read, decide, write" can act
on data that changed between the read and the write.

**Serializable**: behaves as if each transaction ran alone. Postgres detects
when that can't be honored and fails the transaction with SQLSTATE `40001`
rather than let a serialization anomaly through.

```csharp
await using var transaction = await context.Database
    .BeginTransactionAsync(IsolationLevel.Serializable);
```

**When to Use**:
- Read Committed: most day-to-day CRUD
- Serializable + retry loop: financial calculations, inventory counts,
  anything where "check, then act" must be trustworthy under concurrency

---

### 5. Unit of Work

**Problem**: every operation from Parts 2-5 repeats "begin transaction, do
work, commit or rollback."

**Solution**: factor it out once.

```csharp
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);
}
```

**When to Use**:
- Any codebase with more than one multi-step, transactional operation

## Best Practices

### Explicit Transactions
✅ Wrap ALL the `SaveChanges()` calls in one business operation
✅ Always `RollbackAsync()` (or dispose without committing) on failure
❌ Don't leave a transaction open across user input or network calls to other systems

### Savepoints
✅ Name them meaningfully (`"debit-leg"`, not `"sp1"`)
✅ Reload any entities whose failed change needs discarding
❌ Don't forget: without a savepoint, ANY failed statement aborts the whole transaction

### Optimistic Concurrency
✅ Always handle `DbUpdateConcurrencyException` somewhere in the call stack
✅ Reload before retrying, so the retry acts on current data
❌ Don't retry blindly without reloading -- you'll just fail the same way again

### Isolation Levels
✅ Default to Read Committed; opt into Serializable deliberately
✅ Always pair Serializable with a retry loop
❌ Don't use Serializable everywhere "to be safe" -- it costs throughput and forces retry handling everywhere

### Unit of Work
✅ Keep it generic (any operation, any isolation level)
✅ Let domain-specific exceptions (like `InsufficientFundsException`) propagate through it
❌ Don't let it know about specific business operations -- that belongs one layer up

## Testing Considerations

### Explicit Transactions and Savepoints
```csharp
// Real assertions against a real, disposable Postgres container.
await act.Should().ThrowAsync<InvalidOperationException>();
aliceAfter.Balance.Should().Be(500m); // rollback verified against the DB
```

### Optimistic Concurrency
```csharp
// Two genuinely separate DbContext instances, not one queried twice.
await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
```

### Isolation Levels
```csharp
// A deterministic conflict: A reads, B commits, THEN A writes.
SerializationRetryPolicy.IsSerializationFailure(caught).Should().BeTrue();
```

## Next Steps

After completing EfCoreTransactions:

1. **Review all five EF Core modules**
   - EfCoreModeling: entities and relationships
   - EfCoreMigrations: evolving the schema over time
   - EfCoreQuerying: LINQ, projections, PostgreSQL-specific querying
   - EfCoreTransactions: this project
   - EfCoreLoggingAndHealthChecks: seeing what EF Core actually sends, and
     exposing its health

2. **Apply to real projects**
   - Find every place a business operation calls `SaveChanges()` more than
     once, and check it's wrapped in a transaction
   - Find every row two processes might update concurrently, and add a
     concurrency token

## Checklist

After this project, you should be able to:

- [ ] Explain why a single `SaveChanges()` call is already atomic
- [ ] Wrap a multi-`SaveChanges()` operation in an explicit transaction
- [ ] Use savepoints to undo one step without aborting a transaction
- [ ] Map `xmin` as a concurrency token and handle `DbUpdateConcurrencyException`
- [ ] Explain the difference between Read Committed and Serializable
- [ ] Detect and retry a `40001` serialization failure
- [ ] Build a small unit-of-work wrapper

---

**Ready to master EF Core transactions?** Open [EXERCISE.md](EXERCISE.md)!
