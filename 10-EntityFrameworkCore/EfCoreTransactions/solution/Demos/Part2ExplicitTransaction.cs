using EfCoreTransactions.Data;
using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Demos;

public static class Part2ExplicitTransaction
{
    public static async Task RunAsync(string connectionString)
    {
        Console.WriteLine("=== PART 2: EXPLICIT TRANSACTIONS ===\n");

        await RunWithoutTransactionAsync(connectionString);
        Console.WriteLine();
        await RunWithTransactionAsync(connectionString);
    }

    /// <summary>
    /// Two separate SaveChanges() calls -- debit, then credit -- with
    /// NO explicit transaction wrapping them. A failure between the two
    /// leaves the debit permanently persisted with no matching credit.
    /// </summary>
    private static async Task RunWithoutTransactionAsync(string connectionString)
    {
        Console.WriteLine("-- Two SaveChanges() calls, no explicit transaction --");

        await using var context = await DemoSupport.CreateFreshContextAsync(connectionString);
        var alice = new Account { Owner = "Alice", Balance = 500m };
        var bob = new Account { Owner = "Bob", Balance = 100m };
        context.Accounts.AddRange(alice, bob);
        await context.SaveChangesAsync();

        try
        {
            alice.Balance -= 200m;
            await context.SaveChangesAsync(); // SaveChanges #1: the debit, committed on its own.

            SimulateDownstreamFailure();

            bob.Balance += 200m;
            await context.SaveChangesAsync(); // SaveChanges #2: never reached.
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Caught: {ex.Message}");
        }

        await using var verify = new BankDbContext(DemoSupport.BuildOptions(connectionString));
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == alice.Id);
        var bobAfter = await verify.Accounts.SingleAsync(a => a.Id == bob.Id);
        Console.WriteLine($"Alice={aliceAfter.Balance:C} (debited), Bob={bobAfter.Balance:C} (never credited) -- $200 has gone missing.");
    }

    /// <summary>
    /// The same two SaveChanges() calls, now wrapped in an explicit
    /// transaction. The same simulated failure rolls BOTH of them back,
    /// leaving balances untouched -- this is the core reason to reach for
    /// Database.BeginTransactionAsync() once a business operation needs more
    /// than one SaveChanges() call.
    /// </summary>
    private static async Task RunWithTransactionAsync(string connectionString)
    {
        Console.WriteLine("-- Same two SaveChanges() calls, wrapped in an explicit transaction --");

        await using var context = await DemoSupport.CreateFreshContextAsync(connectionString);
        var alice = new Account { Owner = "Alice", Balance = 500m };
        var bob = new Account { Owner = "Bob", Balance = 100m };
        context.Accounts.AddRange(alice, bob);
        await context.SaveChangesAsync();

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            alice.Balance -= 200m;
            await context.SaveChangesAsync(); // debit -- NOT yet committed, still inside the transaction.

            SimulateDownstreamFailure();

            bob.Balance += 200m;
            await context.SaveChangesAsync(); // never reached.

            await transaction.CommitAsync();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Caught: {ex.Message}");
            await transaction.RollbackAsync();

            // Record the failed attempt for audit purposes -- deliberately
            // in a NEW, separate transaction/SaveChanges, since the one that
            // just rolled back can no longer be used to persist anything.
            await LogFailedTransferAsync(connectionString, alice.Id, bob.Id, 200m);
        }

        await using var verify = new BankDbContext(DemoSupport.BuildOptions(connectionString));
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == alice.Id);
        var bobAfter = await verify.Accounts.SingleAsync(a => a.Id == bob.Id);
        var failedCount = await verify.Transfers.CountAsync(t => t.Status == TransferStatus.Failed);
        Console.WriteLine($"Alice={aliceAfter.Balance:C}, Bob={bobAfter.Balance:C} -- rollback undid the debit too, nothing lost.");
        Console.WriteLine($"{failedCount} Failed transfer row(s) recorded for audit, outside the rolled-back transaction.");
    }

    private static async Task LogFailedTransferAsync(string connectionString, int fromAccountId, int toAccountId, decimal amount)
    {
        await using var auditContext = new BankDbContext(DemoSupport.BuildOptions(connectionString));
        auditContext.Transfers.Add(new Transfer
        {
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = amount,
            Status = TransferStatus.Failed
        });
        await auditContext.SaveChangesAsync();
    }

    private static void SimulateDownstreamFailure() =>
        throw new InvalidOperationException("Simulated failure between the debit and credit SaveChanges() calls (e.g. a crash, a downstream service throwing).");
}
