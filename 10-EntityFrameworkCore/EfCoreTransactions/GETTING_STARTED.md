# Getting Started with EfCoreTransactions

## Quick Start

### 1. Start Postgres for the Module
This project needs a real PostgreSQL server -- there's no SQLite fallback,
because savepoints, `xmin`, and `40001` serialization failures are all
Postgres-specific behavior.

```bash
cd 10-EntityFrameworkCore
docker compose up -d
```

This provisions `efcore_transactions` (and the other EF Core module
databases) on `localhost:5432`, user `postgres`, password `postgres`. Stop it
later with `docker compose down`, or `docker compose down -v` to also wipe
the data volume.

### 2. Navigate to the Project
```bash
cd 10-EntityFrameworkCore/EfCoreTransactions
```

### 3. Verify Setup
```bash
dotnet build EfCoreTransactions
```

### 4. What Is Already Here

```
EfCoreTransactions/
├── README.md                  # The concepts behind each part
├── EXERCISE.md                # The work, 6 parts + setup
├── GETTING_STARTED.md         # This file
│
├── EfCoreTransactions/        # <- YOUR WORKSPACE. Write your code here.
│   ├── EfCoreTransactions.csproj  #   ready to build
│   ├── Program.cs             #   replace as you work through each part
│   └── appsettings.json       #   already points at efcore_transactions
│
├── solution/                  # <- REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Domain/                #   Account, Transfer, TransferStatus
│   ├── Data/                  #   BankDbContext, configurations, SnakeCaseNaming
│   ├── Services/               #   UnitOfWork, TransferService, retry policy
│   └── Demos/                 #   one runnable demo per part
│
└── tests/                     # <- proves the behaviour, against real Postgres
```

The workspace project and `appsettings.json` already exist, so you can start
typing as soon as Part 0 tells you to.

### 5. The Commands You Need

```bash
# Run your own work
dotnet run --project EfCoreTransactions

# Check your work against the tests (needs Docker running, but NOT
# `docker compose up` -- the tests start their own throwaway container)
dotnet test tests

# See the reference solution run, one part at a time
dotnet run --project solution -- 1      # Implicit transactions
dotnet run --project solution -- 2      # Explicit transactions
dotnet run --project solution -- 3      # Savepoints
dotnet run --project solution -- 4      # Optimistic concurrency (xmin)
dotnet run --project solution -- 5      # Isolation levels
dotnet run --project solution -- 6      # Unit of work
dotnet run --project solution -- all    # Everything in order
```

### How to Use the Reference Solution

`solution/` uses the same namespaces and type names as `EXERCISE.md`, so you
can compare your file against its counterpart directly (`solution/Data/BankDbContext.cs`
against the `Data/BankDbContext.cs` you wrote, and so on).

Attempt each part yourself first. Open the reference when you're stuck, or
once you've finished a part and want to compare approaches -- reading it up
front is the fastest way to feel productive and learn nothing.

The tests point at `solution/` out of the box. To run them against **your**
code instead, edit the `ProjectReference` in
`tests/EfCoreTransactions.Tests.csproj`:

```xml
<ProjectReference Include="../EfCoreTransactions/EfCoreTransactions.csproj" />
```

They will fail until you've written the types each test needs, which makes
them a usable checklist for how far you've got.

### 6. Start Exercising
Open [EXERCISE.md](EXERCISE.md) and begin with **Part 0: The Domain and the
DbContext**.

## What You've Learned So Far (If You Did the Earlier EF Core Projects)

### From EfCoreModeling
- `IEntityTypeConfiguration<T>` and the Fluent API
- Relationships, owned types, value converters
- snake_case as a whole-model convention

### From EfCoreMigrations
- Creating and applying migrations
- `Database.Migrate()` vs. `dotnet ef database update`

### From EfCoreQuerying
- LINQ filtering, projections, `AsNoTracking`
- PostgreSQL-specific querying

### Now: EfCoreTransactions
- **When** `SaveChanges()` alone is enough, and when it isn't
- **How** to control transactions, savepoints, concurrency, and isolation
  explicitly
- **Why** each of these exists -- what specifically breaks without it

## The Concept Overview

### Quick Reference

| Concept | Solves | Example |
|---|---|---|
| **Implicit transaction** | Atomicity within one `SaveChanges()` | Debit + credit, one call |
| **Explicit transaction** | Atomicity across multiple `SaveChanges()` calls | Debit, then credit, as two calls |
| **Savepoint** | Undoing one step without losing the rest | A multi-step batch, one step fails |
| **`xmin` concurrency token** | Lost updates | Two users editing the same row |
| **Isolation level** | What "concurrent" is allowed to see | Read Committed vs. Serializable |
| **Unit of work** | Repeated transaction boilerplate | Every multi-step business operation |

## How to Approach Each Part

### Part 0: Domain and DbContext

**Goal**: get `Account`, `Transfer`, and `BankDbContext` in place, schema
created.

**Key Concept**: `xmin` is mapped once, in `AccountConfiguration`, and never
touched again -- Postgres and EF Core manage it entirely.

---

### Part 1: Implicit Transactions

**Goal**: see that one `SaveChanges()` call is already atomic.

**Key Concept**:
```csharp
// No BeginTransactionAsync() anywhere -- and it's still atomic.
alice.Balance -= 50m;
bob.Balance += 50m;
await context.SaveChangesAsync();
```

**When You'll Use This**: constantly, without thinking about it -- most
CRUD operations never need more than this.

---

### Part 2: Explicit Transactions

**Goal**: keep two `SaveChanges()` calls atomic together.

**Key Concept**:
```csharp
await using var transaction = await context.Database.BeginTransactionAsync();
// ... SaveChanges(), SaveChanges() ...
await transaction.CommitAsync();  // or RollbackAsync() in a catch
```

**When You'll Use This**: any business operation with more than one write
that must succeed or fail together.

---

### Part 3: Savepoints

**Goal**: undo one step of a transaction without losing the rest.

**Key Concept**:
```csharp
await transaction.CreateSavepointAsync("name");
// ... risky step ...
await transaction.RollbackToSavepointAsync("name"); // on failure
```

**When You'll Use This**: multi-step batch operations where one item's
failure shouldn't discard everything else already validated in the same
transaction.

---

### Part 4: Optimistic Concurrency

**Goal**: stop lost updates without locking rows.

**Key Concept**:
```csharp
try { await context.SaveChangesAsync(); }
catch (DbUpdateConcurrencyException)
{
    await context.Entry(entity).ReloadAsync();
    // reapply your change, try again
}
```

**When You'll Use This**: any row more than one user or process might
update around the same time.

---

### Part 5: Isolation Levels

**Goal**: know what your transaction is allowed to see, and handle it when
Postgres refuses to let an anomaly through.

**Key Concept**:
```csharp
await using var transaction = await context.Database
    .BeginTransactionAsync(IsolationLevel.Serializable);
// catch SQLSTATE 40001, retry the WHOLE operation from scratch
```

**When You'll Use This**: financial totals, inventory counts, anything
where "read, decide, write" needs to be trustworthy under real concurrency.

---

### Part 6: Unit of Work

**Goal**: stop repeating "begin, work, commit or rollback" in every method.

**Key Concept**:
```csharp
await _unitOfWork.ExecuteAsync(async () =>
{
    // multiple SaveChanges() calls here, all-or-nothing
});
```

**When You'll Use This**: any codebase with more than one transactional,
multi-step operation.

## Tips for Success

### 1. Understand the "Why"
Don't just copy code. For each part, ask:
- What specifically goes wrong without this?
- What's the smallest change that would break the demo?

### 2. Run After Each Part
```bash
dotnet run
```
Then query the database yourself (`psql`, a GUI client, whatever you have)
and check the numbers match what the console printed.

### 3. Break Things On Purpose
- Comment out a `RollbackAsync()` call. What happens?
- Comment out `RollbackToSavepointAsync()`. What error do you get?
- Remove the `IsConcurrencyToken()` call. Does the concurrency test still
  fail the way you expect?

### 4. Connect to Real Work
Think about your own projects:
- Where does a business operation call `SaveChanges()` more than once
  without a transaction around it?
- Which rows get edited by more than one person, with no concurrency check?

## Common Patterns

### A Full Transactional Operation

```csharp
public async Task<int> TransferAsync(int fromId, int toId, decimal amount)
{
    return await _unitOfWork.ExecuteAsync(async () =>
    {
        var transfer = new Transfer { FromAccountId = fromId, ToAccountId = toId, Amount = amount };
        _context.Transfers.Add(transfer);
        await _context.SaveChangesAsync();

        var from = await _context.Accounts.SingleAsync(a => a.Id == fromId);
        if (from.Balance < amount) throw new InsufficientFundsException(fromId, amount, from.Balance);
        from.Balance -= amount;
        await _context.SaveChangesAsync();

        var to = await _context.Accounts.SingleAsync(a => a.Id == toId);
        to.Balance += amount;
        await _context.SaveChangesAsync();

        transfer.Status = TransferStatus.Completed;
        await _context.SaveChangesAsync();
        return transfer.Id;
    });
}
```

## What to Watch For

### Explicit Transactions
Watch:
- Every `SaveChanges()` that belongs to the operation is INSIDE the
  transaction
- The `catch` block always rolls back before rethrowing (or the
  `await using` disposal does it for you)

### Savepoints
Watch:
- The savepoint is created BEFORE the risky step, not after
- Entities touched by a rolled-back step get `ReloadAsync()`'d before the
  transaction is used again

### Optimistic Concurrency
Watch:
- `Version` (or whatever you name it) is never set by your own code
- The retry reapplies the INTENT (`Balance -= 50m`), not the stale value

### Isolation Levels
Watch:
- Which statement actually throws (it can be the `UPDATE` itself, or
  `CommitAsync()`, depending on the conflict) -- don't hard-code an
  assumption about where
- The retry runs the WHOLE operation again, including re-reading, not just
  the write

## Troubleshooting

### "Connection refused" / "Npgsql.NpgsqlException: Failed to connect"
- Is Postgres running? `docker compose up -d` from `10-EntityFrameworkCore/`
- Check `appsettings.json`'s connection string matches `docker-compose.yml`

### "relation \"accounts\" does not exist"
- Did `Database.EnsureCreatedAsync()` run? It only builds the schema if it
  doesn't already exist -- if you changed your model, you may need to drop
  and recreate (`docker compose down -v && docker compose up -d`) since this
  exercise doesn't use migrations.

### "current transaction is aborted, commands ignored until end of transaction block"
- A statement inside your transaction failed and you didn't roll back to a
  savepoint (or the whole transaction). See Part 3.

### `DbUpdateConcurrencyException` on every save, even without a real race
- Check `AccountConfiguration`: `Version` must have `HasColumnName("xmin")`,
  `HasColumnType("xid")`, `ValueGeneratedOnAddOrUpdate()`, and
  `IsConcurrencyToken()` all four.

### Tests hang or fail to start
- `dotnet test tests` needs Docker running (for Testcontainers), but NOT
  `docker compose up` -- it starts its own throwaway container per test run.
- This sandbox/environment may not have a Docker daemon at all; in that
  case the tests will fail to start a container, but `dotnet build tests`
  should still succeed.

## Key Concepts to Internalize

### 1. Atomicity Is Per-`SaveChanges()`, Not Per-Method
A C# method with three `SaveChanges()` calls is three transactions unless
you say otherwise.

### 2. A Failed Statement Poisons the Whole Postgres Transaction
Not just that one statement -- everything after it, until you roll back
(fully or to a savepoint).

### 3. Concurrency Tokens Turn Silent Loss Into a Loud Exception
`DbUpdateConcurrencyException` is the feature, not a bug to suppress.

### 4. Serializable Trades Throughput for a Stronger Guarantee
That's why it needs a retry loop -- the "cost" of the guarantee is that
some transactions must be thrown away and redone.

## After Completing This Project

You'll understand:
- When `SaveChanges()` alone is enough, and when it isn't
- How to keep multi-step database operations atomic
- How Postgres's `xmin` gives you optimistic concurrency almost for free
- The practical difference between isolation levels
- How to structure transactional code so it doesn't repeat itself

You'll be able to:
- Wrap any multi-write business operation in a correct transaction
- Recover from a failed statement without losing earlier work
- Detect and handle concurrent edits to the same row
- Choose and defend an isolation level for a given operation
- Build (or recognize the need for) a unit-of-work abstraction

## Next Steps

1. Complete all 6 parts
2. Compare against `solution/`
3. Run `dotnet test tests` if you have Docker available locally
4. Move to [EfCoreLoggingAndHealthChecks](../EfCoreLoggingAndHealthChecks/)

---

**Ready to level up?** Open [EXERCISE.md](EXERCISE.md) and start with Part 0!
