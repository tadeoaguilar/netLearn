# Exercise: Transactions and Concurrency with EF Core + PostgreSQL

## Overview
In this exercise you'll build the classic bank-transfer example: two
`Account` rows, and a `Transfer` that moves money between them. It's the
standard way to teach transactions because the question "what happens if
this fails halfway through?" has a concrete, checkable answer -- a balance
that is or isn't $50 too high.

You'll work through six parts, each adding one piece of transaction control:
a plain `SaveChanges()`, an explicit transaction, a savepoint, optimistic
concurrency via Postgres's `xmin`, isolation levels, and finally a small
unit-of-work wrapper that ties it all together.

## Prerequisites
Postgres must be running for every part of this exercise:
```bash
cd 10-EntityFrameworkCore
docker compose up -d
```
This provisions an `efcore_transactions` database on `localhost:5432` (user
`postgres`, password `postgres`). Your workspace's `appsettings.json` already
points at it.

## Learning Goals
By completing this exercise, you will:
- Understand why a single `SaveChanges()` call is already atomic, with no
  transaction code of your own
- Use `Database.BeginTransactionAsync()` / `CommitAsync()` / `RollbackAsync()`
  to keep a multi-`SaveChanges()` operation atomic
- Use savepoints to undo one step of a transaction without aborting the whole
  thing
- Map Postgres's `xmin` system column as an EF Core concurrency token, catch
  `DbUpdateConcurrencyException`, and implement reload-and-retry
- Tell Read Committed and Serializable isolation apart by what each one lets
  through, and retry a genuine `40001` serialization failure
- Factor "begin transaction, do the work, commit or rollback" into a reusable
  unit-of-work wrapper

---

## Part 0: The Domain and the DbContext

### Step 0.1: The Entities

**Your Task:**
Create `Domain/TransferStatus.cs`:

```csharp
namespace EfCoreTransactions.Domain;

public enum TransferStatus
{
    Pending,
    Completed,
    Failed
}
```

Create `Domain/Account.cs`:

```csharp
namespace EfCoreTransactions.Domain;

public class Account
{
    public int Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public decimal Balance { get; set; }

    // Maps onto Postgres's xmin system column -- see AccountConfiguration.
    // Never set this yourself.
    public uint Version { get; set; }
}
```

Create `Domain/Transfer.cs`:

```csharp
namespace EfCoreTransactions.Domain;

public class Transfer
{
    public int Id { get; set; }
    public int FromAccountId { get; set; }
    public int ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TransferStatus Status { get; set; } = TransferStatus.Pending;
}
```

**Why a separate `Transfer` row?** If you only mutated two `Balance`
columns, a rollback would leave nothing to inspect. A `Transfer` row gives
Parts 2 and 6 something to check afterward: did it end up `Completed`, or is
there no trace of it at all?

### Step 0.2: snake_case Naming

PostgreSQL folds unquoted identifiers to lower case, so a column mapped as
`OwnerId` can only ever be referenced as `"OwnerId"` -- quoted, every time.
This repository's convention (see module 09) is to rename the whole schema
to snake_case as an EF Core convention, applied once, last.

**Your Task:**
Create `Data/SnakeCaseNaming.cs` by copying it verbatim (only the namespace
changes) from
`09-EnterpriseCRUD/src/TaskManagement.Infrastructure/Persistence/SnakeCaseNaming.cs`.
Change its `namespace` line to `namespace EfCoreTransactions.Data;`.

### Step 0.3: Entity Configurations

**Your Task:**
Create `Data/Configurations/AccountConfiguration.cs`:

```csharp
using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreTransactions.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        // A real CHECK constraint, enforced by Postgres -- Part 3 uses it.
        builder.ToTable("accounts", t => t.HasCheckConstraint(
            "ck_accounts_balance_non_negative", "balance >= 0"));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Owner)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Balance)
            .HasColumnType("decimal(18,2)");

        // Map the CLR property onto Postgres's xmin system column and tell
        // EF Core to treat it as a concurrency token. xmin is a 32-bit
        // unsigned transaction id -- hence uint and the "xid" column type.
        // ValueGeneratedOnAddOrUpdate means EF Core never writes to this
        // column, only reads it back and compares it on the next
        // UPDATE/DELETE's WHERE clause.
        builder.Property(a => a.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
```

Create `Data/Configurations/TransferConfiguration.cs`:

```csharp
using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreTransactions.Data.Configurations;

public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount)
            .HasColumnType("decimal(18,2)");

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

**Question to think about:** Why does `HasColumnName("xmin")` have to run
*before* `UseSnakeCaseNames()` in the model-building pipeline, and why does
it not matter here? (Hint: what does `ToSnakeCase("xmin")` return?)

### Step 0.4: The DbContext

**Your Task:**
Create `Data/BankDbContext.cs`:

```csharp
using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Data;

public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankDbContext).Assembly);

        // Applied LAST, so it renames whatever the configurations produced.
        modelBuilder.UseSnakeCaseNames();
    }
}
```

### Step 0.5: Wire It Up

**Your Task:**
Update `Program.cs`:

```csharp
using EfCoreTransactions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connectionString = configuration.GetConnectionString("BankDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:BankDb");

var options = new DbContextOptionsBuilder<BankDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var context = new BankDbContext(options);

// EnsureCreated() builds the schema from your model directly -- fine for an
// exercise like this one. A real app would use migrations (see
// EfCoreMigrations).
await context.Database.EnsureCreatedAsync();

Console.WriteLine("Schema ready. Now build Part 1 below.");
```

**Run it:**
```bash
dotnet run
```
If this connects and prints the message, your schema exists in
`efcore_transactions` and you're ready for Part 1.

---

## Part 1: Implicit Transactions

### Why This Comes First
Before reaching for `BeginTransactionAsync()`, it's worth seeing that you
often don't need it: a single call to `SaveChanges()` is *already* a
transaction for everything it's tracking, with zero transaction code of your
own.

### Step 1.1: One SaveChanges(), Two Changes

**Your Task:**
Replace the bottom of `Program.cs` (after schema creation) with:

```csharp
using EfCoreTransactions.Domain;

var alice = new Account { Owner = "Alice", Balance = 500m };
var bob = new Account { Owner = "Bob", Balance = 100m };
context.Accounts.AddRange(alice, bob);
await context.SaveChangesAsync();

Console.WriteLine($"Before: Alice={alice.Balance:C}, Bob={bob.Balance:C}");

// ONE SaveChanges() call, but TWO tracked changes -- the debit and the
// credit. EF Core opens a transaction, sends both UPDATEs inside it, and
// commits. No BeginTransactionAsync() anywhere in this method.
alice.Balance -= 50m;
bob.Balance += 50m;
await context.SaveChangesAsync();

Console.WriteLine($"After:  Alice={alice.Balance:C}, Bob={bob.Balance:C}");
```

**Run it:**
```bash
dotnet run
```

**Questions to think about:**
1. If the credit's UPDATE statement failed (say, a constraint violation),
   would the debit still be persisted? Why or why not?
2. What's the practical limit of implicit transactions -- what happens the
   moment your business operation needs to call `SaveChanges()` *twice*?

---

## Part 2: Explicit Transactions

### The Problem Part 1 Doesn't Solve
The moment an operation needs two separate `SaveChanges()` calls -- say,
because something has to happen *between* the debit and the credit -- you no
longer get atomicity for free. Each `SaveChanges()` is its own transaction by
default. A failure between them leaves the first one permanently committed.

### Step 2.1: See the Anti-Pattern Fail

**Your Task:**
Add this method to `Program.cs` (or a new file) and call it:

```csharp
static async Task WithoutTransactionAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(connectionString).Options;
    await using var context = new BankDbContext(options);

    var alice = new Account { Owner = "Alice2", Balance = 500m };
    var bob = new Account { Owner = "Bob2", Balance = 100m };
    context.Accounts.AddRange(alice, bob);
    await context.SaveChangesAsync();

    try
    {
        alice.Balance -= 200m;
        await context.SaveChangesAsync(); // SaveChanges #1: the debit, committed on its own.

        throw new InvalidOperationException("Simulated failure -- e.g. a crash, a downstream call throwing.");

        bob.Balance += 200m;
        await context.SaveChangesAsync(); // never reached
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Caught: {ex.Message}");
    }

    await using var verify = new BankDbContext(options);
    var a = await verify.Accounts.SingleAsync(x => x.Id == alice.Id);
    var b = await verify.Accounts.SingleAsync(x => x.Id == bob.Id);
    Console.WriteLine($"Alice={a.Balance:C} (debited), Bob={b.Balance:C} (never credited) -- $200 has gone missing.");
}
```

(Delete the unreachable-code compiler error by removing the lines after
`throw`, or wrap them so the compiler doesn't flag it -- the point stands
either way: whatever runs before the throw commits, whatever runs after
doesn't.)

**Run it and observe:** Alice is down $200. Bob never got it.

### Step 2.2: Fix It with an Explicit Transaction

**Your Task:**
Write the same operation, this time wrapped in a transaction:

```csharp
static async Task WithTransactionAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(connectionString).Options;
    await using var context = new BankDbContext(options);

    var alice = new Account { Owner = "Alice3", Balance = 500m };
    var bob = new Account { Owner = "Bob3", Balance = 100m };
    context.Accounts.AddRange(alice, bob);
    await context.SaveChangesAsync();

    await using var transaction = await context.Database.BeginTransactionAsync();
    try
    {
        alice.Balance -= 200m;
        await context.SaveChangesAsync(); // debit -- NOT committed yet, still inside the transaction

        SimulateFailure(); // same simulated failure as before

        bob.Balance += 200m;
        await context.SaveChangesAsync(); // never reached

        await transaction.CommitAsync();
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Caught: {ex.Message}");
        await transaction.RollbackAsync();
    }

    await using var verify = new BankDbContext(options);
    var a = await verify.Accounts.SingleAsync(x => x.Id == alice.Id);
    var b = await verify.Accounts.SingleAsync(x => x.Id == bob.Id);
    Console.WriteLine($"Alice={a.Balance:C}, Bob={b.Balance:C} -- rollback undid the debit too.");
}

static void SimulateFailure() =>
    throw new InvalidOperationException("Simulated failure between the debit and credit SaveChanges() calls.");
```

**Run it and observe:** this time Alice's balance is untouched -- the
rollback undid the already-committed-to-the-transaction debit along with
skipping the credit.

**This is the core teaching moment of the whole exercise:** the instant a
business operation spans more than one `SaveChanges()` call, you need
`BeginTransactionAsync()` / `CommitAsync()` / `RollbackAsync()` around all of
it, or a failure partway through leaves the database in a state nobody
intended.

**Questions to think about:**
1. What would happen if you called `transaction.RollbackAsync()` but forgot
   the `try`/`catch` -- i.e., let the exception propagate past the
   `using`/`await using` block without ever calling `RollbackAsync()`? (Try
   it. What does disposing an uncommitted transaction do?)
2. Where would you record that this transfer *failed*, given that rolling
   back the transaction also erases any `Transfer` row you inserted inside
   it?

---

## Part 3: Savepoints

### The Problem
Inside a Postgres transaction, once *any* statement fails, the entire
transaction is put into an aborted state -- every later command, even a
plain `SELECT`, fails with `current transaction is aborted` until you either
roll back everything or roll back to a savepoint. A savepoint lets you undo
just one step and keep going.

### Step 3.1: A Multi-Step Transfer, One Step Fails

**Your Task:**

```csharp
static async Task SavepointsAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(connectionString).Options;
    await using var context = new BankDbContext(options);

    var alice = new Account { Owner = "AliceSp", Balance = 300m };
    var bob = new Account { Owner = "BobSp", Balance = 50m };
    var carol = new Account { Owner = "CarolSp", Balance = 20m };
    context.Accounts.AddRange(alice, bob, carol);
    await context.SaveChangesAsync();

    await using var transaction = await context.Database.BeginTransactionAsync();

    // Step 1: Alice -> Bob, $100. Succeeds.
    alice.Balance -= 100m;
    bob.Balance += 100m;
    await context.SaveChangesAsync();
    Console.WriteLine("Step 1 applied.");

    // Step 2: Alice -> Carol, $500. Alice only has $200 left -- the
    // "balance >= 0" CHECK constraint rejects this UPDATE.
    await transaction.CreateSavepointAsync("step2");
    try
    {
        alice.Balance -= 500m;
        carol.Balance += 500m;
        await context.SaveChangesAsync();
    }
    catch (DbUpdateException ex)
    {
        Console.WriteLine($"Step 2 rejected: {ex.InnerException?.Message ?? ex.Message}");
        await transaction.RollbackToSavepointAsync("step2");

        // The failed attempt left alice/carol's in-memory values out of
        // sync with the database. Reload before continuing to use the
        // transaction.
        await context.Entry(alice).ReloadAsync();
        await context.Entry(carol).ReloadAsync();
    }

    // Step 3: Bob -> Carol, $30. Succeeds, in the SAME transaction that just
    // recovered from step 2's failure.
    bob.Balance -= 30m;
    carol.Balance += 30m;
    await context.SaveChangesAsync();
    Console.WriteLine("Step 3 applied.");

    await transaction.CommitAsync();

    await using var verify = new BankDbContext(options);
    Console.WriteLine($"Alice={(await verify.Accounts.SingleAsync(x => x.Id == alice.Id)).Balance:C}");
    Console.WriteLine($"Bob=  {(await verify.Accounts.SingleAsync(x => x.Id == bob.Id)).Balance:C}");
    Console.WriteLine($"Carol={(await verify.Accounts.SingleAsync(x => x.Id == carol.Id)).Balance:C}");
}
```

**Run it and observe:** Alice ends at $200 (step 1 kept, step 2 undone),
Bob at $120, Carol at $50 -- step 2 never reached her.

**Questions to think about:**
1. Comment out the `RollbackToSavepointAsync` call (but keep the `catch`).
   What happens when step 3 tries to run? What does the exception say?
2. Why do `alice` and `carol` need `ReloadAsync()` after the rollback, but
   `bob` doesn't?

---

## Part 4: Optimistic Concurrency

### The Problem
Two users load the same account, both compute a new balance from what they
read, and both save. Without a concurrency check, the second save silently
overwrites the first user's change -- a classic lost update.

### Step 4.1: Force a Conflict, Then Recover

**Your Task:**

```csharp
static async Task ConcurrencyAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(connectionString).Options;

    await using var setup = new BankDbContext(options);
    var seed = new Account { Owner = "Concurrent", Balance = 1000m };
    setup.Accounts.Add(seed);
    await setup.SaveChangesAsync();
    var accountId = seed.Id;

    // Two independent DbContext instances -- two concurrent requests that
    // both loaded the SAME row.
    await using var contextA = new BankDbContext(options);
    await using var contextB = new BankDbContext(options);

    var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);
    var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
    Console.WriteLine($"Both read xmin(A)={accountA.Version}, xmin(B)={accountB.Version}");

    accountA.Balance += 100m;
    await contextA.SaveChangesAsync();
    Console.WriteLine($"A saved first. New xmin(A)={accountA.Version}.");

    accountB.Balance -= 50m;
    try
    {
        // B still carries the ORIGINAL xmin. EF Core includes it in the
        // UPDATE's WHERE clause, so this UPDATE matches zero rows -- the
        // server-side xmin already moved.
        await contextB.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        Console.WriteLine("B: DbUpdateConcurrencyException -- someone else changed this row first.");

        // Reload-and-retry.
        await contextB.Entry(accountB).ReloadAsync();
        accountB.Balance -= 50m;
        await contextB.SaveChangesAsync();
        Console.WriteLine($"Retry succeeded. Balance={accountB.Balance:C}");
    }
}
```

**Run it and observe:** the exception is thrown, caught, and the retry
succeeds -- final balance is `1000 + 100 - 50 = 1050`.

**Questions to think about:**
1. Why `uint` for `Version`, not `int` or `long`?
2. `ReloadAsync()` discards `accountB`'s in-memory `Balance` change along
   with its stale `Version`. Why does the retry have to *reapply*
   `accountB.Balance -= 50m` after reloading, instead of just calling
   `SaveChangesAsync()` again?

---

## Part 5: Isolation Levels

### Read Committed vs. Serializable
Read Committed (the default for both Postgres and EF Core) lets a
transaction see other transactions' committed changes immediately, even
mid-transaction. Serializable makes each transaction behave as if it ran
completely alone -- which means Postgres must sometimes *refuse* to let a
transaction commit, because honoring it would prove that isolation a lie.

### Step 5.1: Read Committed Sees Concurrent Commits

**Your Task:**

```csharp
using System.Data;

static async Task ReadCommittedAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(connectionString).Options;
    await using var setup = new BankDbContext(options);
    var seed = new Account { Owner = "RC", Balance = 1000m };
    setup.Accounts.Add(seed);
    await setup.SaveChangesAsync();
    var accountId = seed.Id;

    await using var contextA = new BankDbContext(options);
    await using var transactionA = await contextA.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);

    await using (var contextB = new BankDbContext(options))
    {
        var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
        accountB.Balance += 200m;
        await contextB.SaveChangesAsync();
    }

    await contextA.Entry(accountA).ReloadAsync();
    Console.WriteLine($"A sees B's committed change mid-transaction: Balance={accountA.Balance:C}");
    await transactionA.CommitAsync();
}
```

### Step 5.2: Serializable Refuses, and You Retry

**Your Task:**
First, a small retry helper. Create `Services/SerializationRetryPolicy.cs`:

```csharp
using Npgsql;

namespace EfCoreTransactions.Services;

public static class SerializationRetryPolicy
{
    public const string SerializationFailureSqlState = "40001";

    public static bool IsSerializationFailure(Exception exception)
    {
        var current = exception;
        while (current is not null)
        {
            if (current is PostgresException { SqlState: SerializationFailureSqlState })
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }

    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation, int maxAttempts = 5, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsSerializationFailure(ex) && attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }
}
```

Then force a real conflict and retry through it:

```csharp
static async Task SerializableAsync(string connectionString)
{
    var options = new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(connectionString).Options;
    await using var setup = new BankDbContext(options);
    var seed = new Account { Owner = "Ser", Balance = 1000m };
    setup.Accounts.Add(seed);
    await setup.SaveChangesAsync();
    var accountId = seed.Id;

    var attempts = 0;
    var conflictInjected = false;

    var finalBalance = await SerializationRetryPolicy.ExecuteAsync(async () =>
    {
        attempts++;
        await using var contextA = new BankDbContext(options);
        await using var transactionA = await contextA.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Establishes A's snapshot.
        var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);

        if (!conflictInjected)
        {
            conflictInjected = true;

            // Simulates a genuinely concurrent transaction committing a
            // change to the SAME row while A's snapshot is open. Only
            // happens once, so the retry has nothing racing it.
            await using var contextB = new BankDbContext(options);
            var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
            accountB.Balance += 10m;
            await contextB.SaveChangesAsync();
        }

        // A tries to write a row it read BEFORE B's commit. Postgres
        // refuses with SQLSTATE 40001 rather than let this silently through.
        accountA.Balance -= 25m;
        await contextA.SaveChangesAsync();
        await transactionA.CommitAsync();

        return accountA.Balance;
    });

    Console.WriteLine($"Succeeded after {attempts} attempt(s). Balance={finalBalance:C}");
}
```

**Run it and observe:** the console shows "Attempt 1" fail and "Attempt 2"
succeed (add a `Console.WriteLine($"Attempt {attempts}...")` inside the
lambda if you want to see it directly).

**Questions to think about:**
1. Why does injecting B's write only on the *first* attempt matter for this
   demo? What would happen if it ran on every attempt?
2. `ExecuteAsync`'s `catch` filter is `when (IsSerializationFailure(ex) &&
   attempt < maxAttempts)`. What happens to an exception that fails that
   filter -- does it get swallowed, or does it propagate? Why is that the
   right behavior for, say, an `InsufficientFundsException`?

---

## Part 6: A Unit-of-Work Wrapper

### Why
Parts 2 through 5 all repeat the same shape: begin a transaction, do some
work, commit on success, roll back on failure. Once you notice that pattern
three or four times, it's worth factoring out.

### Step 6.1: `IUnitOfWork`

**Your Task:**
Create `Services/IUnitOfWork.cs`:

```csharp
using System.Data;

namespace EfCoreTransactions.Services;

public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        Func<Task> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}
```

Create `Services/UnitOfWork.cs`:

```csharp
using System.Data;
using EfCoreTransactions.Data;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Services;

public class UnitOfWork : IUnitOfWork
{
    private readonly BankDbContext _context;

    public UnitOfWork(BankDbContext context) => _context = context;

    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task ExecuteAsync(
        Func<Task> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () => { await operation(); return true; }, isolationLevel, cancellationToken);
}
```

### Step 6.2: `ITransferService`

**Your Task:**
Create `Services/InsufficientFundsException.cs`:

```csharp
namespace EfCoreTransactions.Services;

public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(int accountId, decimal requestedAmount, decimal availableBalance)
        : base($"Account {accountId} has insufficient funds: requested {requestedAmount:C}, available {availableBalance:C}.")
    {
        AccountId = accountId;
        RequestedAmount = requestedAmount;
        AvailableBalance = availableBalance;
    }

    public int AccountId { get; }
    public decimal RequestedAmount { get; }
    public decimal AvailableBalance { get; }
}
```

Create `Services/ITransferService.cs`:

```csharp
namespace EfCoreTransactions.Services;

public interface ITransferService
{
    Task<int> TransferAsync(
        int fromAccountId, int toAccountId, decimal amount, CancellationToken cancellationToken = default);
}
```

Create `Services/TransferService.cs`:

```csharp
using EfCoreTransactions.Data;
using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Services;

public class TransferService : ITransferService
{
    private readonly BankDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public TransferService(BankDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public Task<int> TransferAsync(
        int fromAccountId, int toAccountId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Transfer amount must be positive.");
        if (fromAccountId == toAccountId)
            throw new ArgumentException("An account cannot transfer to itself.", nameof(toAccountId));

        return _unitOfWork.ExecuteAsync(async () =>
        {
            var transfer = new Transfer
            {
                FromAccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = amount,
                Status = TransferStatus.Pending
            };
            _context.Transfers.Add(transfer);
            await _context.SaveChangesAsync(cancellationToken);

            var from = await _context.Accounts.SingleAsync(a => a.Id == fromAccountId, cancellationToken);
            if (from.Balance < amount)
                throw new InsufficientFundsException(fromAccountId, amount, from.Balance);

            from.Balance -= amount;
            await _context.SaveChangesAsync(cancellationToken); // debit leg

            var to = await _context.Accounts.SingleAsync(a => a.Id == toAccountId, cancellationToken);
            to.Balance += amount;
            await _context.SaveChangesAsync(cancellationToken); // credit leg

            transfer.Status = TransferStatus.Completed;
            await _context.SaveChangesAsync(cancellationToken);

            return transfer.Id;
        }, cancellationToken: cancellationToken);
    }
}
```

### Step 6.3: Use It

**Your Task:**

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddDbContext<BankDbContext>(o => o.UseNpgsql(connectionString));
services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddScoped<ITransferService, TransferService>();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var transferService = scope.ServiceProvider.GetRequiredService<ITransferService>();

var id = await transferService.TransferAsync(aliceId, bobId, 150m);
Console.WriteLine($"Transfer #{id} completed.");

try
{
    await transferService.TransferAsync(aliceId, bobId, 10_000m);
}
catch (InsufficientFundsException ex)
{
    Console.WriteLine($"Rejected: {ex.Message}");
}
```

**Run it and observe:** the first call succeeds; the second throws and,
because the whole operation ran inside `UnitOfWork.ExecuteAsync`, leaves
**no** `Transfer` row behind at all -- not even a `Pending` one.

**Questions to think about:**
1. `TransferService` no longer contains a single `try`/`catch`/rollback of
   its own -- `UnitOfWork` owns all of that. What did you gain by moving it
   out? What, if anything, did you lose?
2. Because the whole operation (including the `Transfer` row's initial
   insert) rolls back on failure, there's no audit trail of a *rejected*
   transfer inside the database. Compare this to what Part 2's demo did
   instead. Which trade-off would you want in a real payments system?

---

## Checking Your Work

A complete reference implementation lives in `solution/`, organized as one
file per part under `solution/Demos/`, and `tests/` proves the behavior
against a real, disposable Postgres container (via Testcontainers).

```bash
dotnet run --project solution -- 1    # watch each part run
dotnet run --project solution -- all
dotnet test tests                     # needs Docker running (not docker compose)
```

Build your own version of each part first. When you compare, notice in
particular:
- Part 2's `solution` also logs a `Failed` transfer for audit purposes, in a
  *separate* transaction, after the real one has already rolled back.
- Part 5's `solution` demo shows Read Committed and Serializable back to
  back, so you can see the same setup behave two different ways.

---

## Reflection Questions

1. **Why is a single `SaveChanges()` call already atomic, and what
   specifically breaks that guarantee once you split a business operation
   across two calls?**

2. **What does a savepoint actually undo -- just your C# entities' state, or
   something the database itself is tracking? How do you know?**

3. **`xmin` is server-managed and Postgres bumps it on every UPDATE,
   regardless of whether the UPDATE changed the row's business data. What
   does that imply about `DbUpdateConcurrencyException` firing even when two
   concurrent writers happen to compute the exact same new value?**

4. **Read Committed re-reads see the latest committed data; Serializable
   transactions instead fail outright rather than let that happen. Why is
   "just re-read and continue" not a safe substitute for what Serializable
   guarantees?**

5. **Where would you draw the line between logic that belongs inside
   `TransferService` and logic that belongs inside `UnitOfWork`?**

---

## Summary

You've learned:
- ✅ Why a single `SaveChanges()` is already atomic
- ✅ Explicit transactions across multiple `SaveChanges()` calls
- ✅ Savepoints, and why Postgres needs them after a failed statement
- ✅ Optimistic concurrency via `xmin`, and reload-and-retry
- ✅ Read Committed vs. Serializable, and retrying a `40001`
- ✅ A reusable unit-of-work wrapper

## Next Steps
This is currently the last project in this module. Move on to
[EfCoreLoggingAndHealthChecks](../EfCoreLoggingAndHealthChecks/) to see what
EF Core was actually sending to Postgres this whole time, or revisit
[09-EnterpriseCRUD](../../09-EnterpriseCRUD/) and look at how its
`ApplicationDbContext.SaveChangesAsync` override interacts with everything
you just learned about transactions.

---

**Happy Learning!**
