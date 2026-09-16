using EfCoreTransactions.Data;
using EfCoreTransactions.Domain;
using EfCoreTransactions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EfCoreTransactions.Demos;

public static class Part6UnitOfWork
{
    public static async Task RunAsync(string connectionString)
    {
        Console.WriteLine("=== PART 6: UNIT OF WORK ===\n");

        var services = new ServiceCollection();
        services.AddDbContext<BankDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITransferService, TransferService>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BankDbContext>();

        await context.Database.EnsureCreatedAsync();
        await DemoSupport.ResetAsync(context);

        var alice = new Account { Owner = "Alice", Balance = 500m };
        var bob = new Account { Owner = "Bob", Balance = 100m };
        context.Accounts.AddRange(alice, bob);
        await context.SaveChangesAsync();

        var transferService = scope.ServiceProvider.GetRequiredService<ITransferService>();

        // Happy path: ITransferService hides the "begin transaction, debit,
        // credit, commit" dance behind one call.
        var transferId = await transferService.TransferAsync(alice.Id, bob.Id, 150m);
        Console.WriteLine($"Transfer #{transferId} completed: Alice={alice.Balance:C}, Bob={bob.Balance:C}.");

        // Failure path: same call, but this time it can't cover the amount.
        // UnitOfWork rolls the whole operation back -- caller just sees the
        // exception, with nothing left half-done to clean up.
        try
        {
            await transferService.TransferAsync(alice.Id, bob.Id, 10_000m);
        }
        catch (InsufficientFundsException ex)
        {
            Console.WriteLine($"Second transfer rejected: {ex.Message}");
        }

        await using var verify = new BankDbContext(DemoSupport.BuildOptions(connectionString));
        var transferCount = await verify.Transfers.CountAsync();
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == alice.Id);
        Console.WriteLine($"Transfers persisted: {transferCount} (the rejected one left no row at all -- the whole unit of work rolled back).");
        Console.WriteLine($"Alice's balance is still {aliceAfter.Balance:C} -- untouched by the failed attempt.");
    }
}
