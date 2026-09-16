using EfCoreTransactions.Data;
using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Demos;

public static class Part3Savepoints
{
    public static async Task RunAsync(string connectionString)
    {
        Console.WriteLine("=== PART 3: SAVEPOINTS ===\n");

        await using var context = await DemoSupport.CreateFreshContextAsync(connectionString);

        var alice = new Account { Owner = "Alice", Balance = 300m };
        var bob = new Account { Owner = "Bob", Balance = 50m };
        var carol = new Account { Owner = "Carol", Balance = 20m };
        context.Accounts.AddRange(alice, bob, carol);
        await context.SaveChangesAsync();

        Console.WriteLine($"Start: Alice={alice.Balance:C}, Bob={bob.Balance:C}, Carol={carol.Balance:C}");

        await using var transaction = await context.Database.BeginTransactionAsync();

        // Step 1: Alice -> Bob, $100. Should succeed and survive to the end.
        await transaction.CreateSavepointAsync("step1");
        alice.Balance -= 100m;
        bob.Balance += 100m;
        await context.SaveChangesAsync();
        Console.WriteLine("Step 1 (Alice -> Bob, $100) applied.");

        // Step 2: Alice -> Carol, $500. Alice only has $200 left, so the
        // "balance >= 0" CHECK constraint rejects the UPDATE at the
        // database. Without a savepoint, ANY statement error puts the whole
        // Postgres transaction into an aborted state -- every later command,
        // even a plain SELECT, fails with "current transaction is aborted"
        // until you either roll back everything or roll back to a
        // savepoint. Rolling back to 'step2' undoes just this step and
        // makes the transaction usable again.
        await transaction.CreateSavepointAsync("step2");
        try
        {
            alice.Balance -= 500m;
            carol.Balance += 500m;
            await context.SaveChangesAsync();
            Console.WriteLine("Step 2 applied.");
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"Step 2 rejected by the database: {ex.InnerException?.Message ?? ex.Message}");
            Console.WriteLine("Rolling back to savepoint 'step2' -- step 1's work is untouched.");
            await transaction.RollbackToSavepointAsync("step2");

            // The failed attempt left alice/carol's in-memory values out of
            // sync with the database (which never actually changed for
            // them). Reload so the change tracker matches reality again
            // before continuing to use the same transaction.
            await context.Entry(alice).ReloadAsync();
            await context.Entry(carol).ReloadAsync();
        }

        // Step 3: Bob -> Carol, $30. Should succeed and be kept, in the SAME
        // transaction that just recovered from step 2's failure.
        await transaction.CreateSavepointAsync("step3");
        bob.Balance -= 30m;
        carol.Balance += 30m;
        await context.SaveChangesAsync();
        Console.WriteLine("Step 3 (Bob -> Carol, $30) applied.");

        await transaction.CommitAsync();

        await using var verify = new BankDbContext(DemoSupport.BuildOptions(connectionString));
        var a = await verify.Accounts.SingleAsync(x => x.Id == alice.Id);
        var b = await verify.Accounts.SingleAsync(x => x.Id == bob.Id);
        var c = await verify.Accounts.SingleAsync(x => x.Id == carol.Id);
        Console.WriteLine($"\nFinal: Alice={a.Balance:C}, Bob={b.Balance:C}, Carol={c.Balance:C}");
        Console.WriteLine("Step 2 was undone by itself; steps 1 and 3 committed together in one transaction.");
    }
}
