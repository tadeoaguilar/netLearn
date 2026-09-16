using EfCoreTransactions.Demos;
using Microsoft.Extensions.Configuration;

// Reference solution for the EfCoreTransactions exercise.
//
// Each part of EXERCISE.md runs on its own so you can compare one section at
// a time against your own work. Needs the module's Postgres running:
//
//     cd 10-EntityFrameworkCore && docker compose up -d
//
//     dotnet run -- 1     # Implicit transactions
//     dotnet run -- 5     # Isolation levels
//     dotnet run -- all   # Everything, in order

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connectionString = configuration.GetConnectionString("BankDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:BankDb in appsettings.json.");

var choice = args.Length > 0 ? args[0] : "menu";

switch (choice)
{
    case "1": await Part1ImplicitTransaction.RunAsync(connectionString); break;
    case "2": await Part2ExplicitTransaction.RunAsync(connectionString); break;
    case "3": await Part3Savepoints.RunAsync(connectionString); break;
    case "4": await Part4OptimisticConcurrency.RunAsync(connectionString); break;
    case "5": await Part5IsolationLevels.RunAsync(connectionString); break;
    case "6": await Part6UnitOfWork.RunAsync(connectionString); break;

    case "all":
        await Part1ImplicitTransaction.RunAsync(connectionString);
        Separator();
        await Part2ExplicitTransaction.RunAsync(connectionString);
        Separator();
        await Part3Savepoints.RunAsync(connectionString);
        Separator();
        await Part4OptimisticConcurrency.RunAsync(connectionString);
        Separator();
        await Part5IsolationLevels.RunAsync(connectionString);
        Separator();
        await Part6UnitOfWork.RunAsync(connectionString);
        break;

    default:
        Console.WriteLine("EfCoreTransactions -- reference solution\n");
        Console.WriteLine("  dotnet run -- 1     Implicit transactions (one SaveChanges, two changes)");
        Console.WriteLine("  dotnet run -- 2     Explicit transactions (commit/rollback across two SaveChanges)");
        Console.WriteLine("  dotnet run -- 3     Savepoints");
        Console.WriteLine("  dotnet run -- 4     Optimistic concurrency (xmin)");
        Console.WriteLine("  dotnet run -- 5     Isolation levels (Read Committed vs. Serializable)");
        Console.WriteLine("  dotnet run -- 6     Unit of work");
        Console.WriteLine("  dotnet run -- all   Run every part in order");
        Console.WriteLine("\nRequires Postgres: cd 10-EntityFrameworkCore && docker compose up -d");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
