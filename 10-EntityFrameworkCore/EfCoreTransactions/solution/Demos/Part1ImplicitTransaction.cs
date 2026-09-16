using EfCoreTransactions.Domain;

namespace EfCoreTransactions.Demos;

public static class Part1ImplicitTransaction
{
    public static async Task RunAsync(string connectionString)
    {
        Console.WriteLine("=== PART 1: IMPLICIT TRANSACTIONS ===\n");

        await using var context = await DemoSupport.CreateFreshContextAsync(connectionString);

        var alice = new Account { Owner = "Alice", Balance = 500m };
        var bob = new Account { Owner = "Bob", Balance = 100m };
        context.Accounts.AddRange(alice, bob);
        await context.SaveChangesAsync();

        Console.WriteLine($"Before: Alice={alice.Balance:C}, Bob={bob.Balance:C}");

        // ONE SaveChanges() call, but TWO tracked changes -- the debit and
        // the credit. EF Core opens a database transaction, sends both
        // UPDATE statements inside it, and commits, all without any
        // transaction code of ours. If either UPDATE were to fail (a
        // constraint violation, a dropped connection), EF Core rolls the
        // whole call back -- you would never observe one balance moved
        // without the other.
        alice.Balance -= 50m;
        bob.Balance += 50m;
        await context.SaveChangesAsync();

        Console.WriteLine($"After:  Alice={alice.Balance:C}, Bob={bob.Balance:C}");
        Console.WriteLine("No explicit transaction code was written -- a single SaveChanges() is already atomic for everything it tracks.");
    }
}
