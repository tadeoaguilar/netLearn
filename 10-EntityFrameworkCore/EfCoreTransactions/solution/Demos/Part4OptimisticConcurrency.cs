using EfCoreTransactions.Data;
using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Demos;

public static class Part4OptimisticConcurrency
{
    public static async Task RunAsync(string connectionString)
    {
        Console.WriteLine("=== PART 4: OPTIMISTIC CONCURRENCY (xmin) ===\n");

        await using var setup = await DemoSupport.CreateFreshContextAsync(connectionString);
        var account = new Account { Owner = "Alice", Balance = 1000m };
        setup.Accounts.Add(account);
        await setup.SaveChangesAsync();
        var accountId = account.Id;

        var options = DemoSupport.BuildOptions(connectionString);

        // Two independent DbContext instances, standing in for two
        // concurrent requests that both loaded the SAME row.
        await using var contextA = new BankDbContext(options);
        await using var contextB = new BankDbContext(options);

        var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);
        var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
        Console.WriteLine($"Both read Balance={accountA.Balance:C}, xmin(A)={accountA.Version}, xmin(B)={accountB.Version}");

        accountA.Balance += 100m;
        await contextA.SaveChangesAsync();
        Console.WriteLine($"Context A saved first -- new xmin(A)={accountA.Version}. The row's xmin has moved on server-side.");

        accountB.Balance -= 50m;
        try
        {
            // contextB still carries the ORIGINAL xmin it read. EF Core puts
            // it in the UPDATE's WHERE clause (roughly
            // `WHERE id = @p AND xmin = @original_xmin`), so this UPDATE
            // matches zero rows -- the server-side xmin no longer matches,
            // because A's save already moved it. EF Core reports "zero rows
            // affected" as a concurrency conflict.
            await contextB.SaveChangesAsync();
            Console.WriteLine("Unexpected: context B saved without a conflict.");
        }
        catch (DbUpdateConcurrencyException)
        {
            Console.WriteLine("Context B: DbUpdateConcurrencyException -- someone else changed this row first.");

            // Reload-and-retry: discard B's stale copy (and its rejected
            // in-memory Balance change), re-read the row's CURRENT state
            // (and current xmin), then reapply B's intent on top of it.
            await contextB.Entry(accountB).ReloadAsync();
            Console.WriteLine($"Reloaded: Balance={accountB.Balance:C}, xmin={accountB.Version}.");

            accountB.Balance -= 50m;
            await contextB.SaveChangesAsync();
            Console.WriteLine($"Retry succeeded: Balance={accountB.Balance:C}, xmin={accountB.Version}.");
        }

        await using var verify = new BankDbContext(options);
        var final = await verify.Accounts.SingleAsync(a => a.Id == accountId);
        Console.WriteLine($"\nFinal balance: {final.Balance:C} (expected {1000m + 100m - 50m:C})");
    }
}
