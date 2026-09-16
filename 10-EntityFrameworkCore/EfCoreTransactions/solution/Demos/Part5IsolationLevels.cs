using System.Data;
using EfCoreTransactions.Data;
using EfCoreTransactions.Domain;
using EfCoreTransactions.Services;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Demos;

public static class Part5IsolationLevels
{
    public static async Task RunAsync(string connectionString)
    {
        Console.WriteLine("=== PART 5: ISOLATION LEVELS ===\n");

        await ReadCommittedDemoAsync(connectionString);
        Console.WriteLine();
        await SerializableConflictDemoAsync(connectionString);
    }

    /// <summary>
    /// Read Committed -- the default for both Postgres and EF Core -- lets a
    /// transaction see other transactions' committed changes as soon as they
    /// commit, even in the middle of its own transaction.
    /// </summary>
    private static async Task ReadCommittedDemoAsync(string connectionString)
    {
        Console.WriteLine("-- Read Committed (the default) --");

        await using var setup = await DemoSupport.CreateFreshContextAsync(connectionString);
        var account = new Account { Owner = "Alice", Balance = 1000m };
        setup.Accounts.Add(account);
        await setup.SaveChangesAsync();
        var accountId = account.Id;

        var options = DemoSupport.BuildOptions(connectionString);

        await using var contextA = new BankDbContext(options);
        await using var transactionA = await contextA.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);

        // While A's transaction is still open, B commits a change to the
        // same row in its own, separate transaction.
        await using (var contextB = new BankDbContext(options))
        {
            var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
            accountB.Balance += 200m;
            await contextB.SaveChangesAsync();
        }

        // Under Read Committed, re-reading mid-transaction sees B's
        // committed change immediately -- unlike Serializable below.
        await contextA.Entry(accountA).ReloadAsync();
        Console.WriteLine($"A re-reads mid-transaction and sees B's committed change: Balance={accountA.Balance:C}.");

        accountA.Balance -= 50m;
        await contextA.SaveChangesAsync();
        await transactionA.CommitAsync();
        Console.WriteLine($"A commits on top of it without any conflict: final Balance={accountA.Balance:C}.");
    }

    /// <summary>
    /// Serializable makes each transaction behave as if it ran alone. Here
    /// A takes its snapshot, B commits a change to the same row, and A then
    /// tries to write that row -- Postgres refuses with SQLSTATE 40001
    /// rather than let A silently overwrite work it never saw. The retry
    /// loop (SerializationRetryPolicy) is the standard way to handle this:
    /// throw the failed attempt away and run the whole operation again.
    /// </summary>
    private static async Task SerializableConflictDemoAsync(string connectionString)
    {
        Console.WriteLine("-- Serializable, forced into a 40001 conflict, with retry --");

        await using var setup = await DemoSupport.CreateFreshContextAsync(connectionString);
        var account = new Account { Owner = "Bob", Balance = 1000m };
        setup.Accounts.Add(account);
        await setup.SaveChangesAsync();
        var accountId = account.Id;

        var options = DemoSupport.BuildOptions(connectionString);
        var attempts = 0;

        // Only inject the competing write on the FIRST attempt -- it models
        // one genuine race, not an operation that can never succeed. The
        // retried attempt has nothing racing it and goes through cleanly.
        var conflictInjected = false;

        var finalBalance = await SerializationRetryPolicy.ExecuteAsync(async () =>
        {
            attempts++;
            Console.WriteLine($"Attempt {attempts}...");

            await using var contextA = new BankDbContext(options);
            await using var transactionA = await contextA.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);

            if (!conflictInjected)
            {
                conflictInjected = true;

                await using var contextB = new BankDbContext(options);
                var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
                accountB.Balance += 10m;
                await contextB.SaveChangesAsync();
            }

            accountA.Balance -= 25m;
            await contextA.SaveChangesAsync();
            await transactionA.CommitAsync();

            return accountA.Balance;
        });

        Console.WriteLine($"Succeeded after {attempts} attempt(s). Balance at the end of the winning attempt: {finalBalance:C}.");

        await using var verify = new BankDbContext(options);
        var final = await verify.Accounts.SingleAsync(a => a.Id == accountId);
        Console.WriteLine($"Verified from a fresh context: {final.Balance:C} (expected {1000m + 10m - 25m:C}).");
    }
}
