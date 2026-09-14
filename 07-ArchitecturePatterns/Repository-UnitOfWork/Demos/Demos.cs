using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RepositoryUnitOfWork.Data;

namespace RepositoryUnitOfWork.Demos;

public static class Demos
{
    private static (ShopDbContext Context, SqliteConnection Connection) NewDatabase()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var context = new ShopDbContext(
            new DbContextOptionsBuilder<ShopDbContext>().UseSqlite(connection).Options);
        context.Database.EnsureCreated();

        return (context, connection);
    }

    public static async Task Part1Repository()
    {
        Console.WriteLine("=== PART 1: THE REPOSITORY ===\n");

        var (context, connection) = NewDatabase();
        await using var unitOfWork = new UnitOfWork(context);

        var customer = new Customer { Name = "Ada", Credit = 500m };
        await unitOfWork.Customers.AddAsync(customer);
        await unitOfWork.SaveChangesAsync();

        var loaded = await unitOfWork.Customers.GetByIdAsync(customer.Id);
        Console.WriteLine($"  round-tripped: {loaded!.Name}, credit {loaded.Credit:C}");

        Console.WriteLine("\nThe caller never saw a DbContext, a DbSet or a LINQ query.");
        Console.WriteLine("Swapping EF Core for Dapper would not change one line above.");

        connection.Dispose();
    }

    public static async Task Part2UnitOfWorkAtomicity()
    {
        Console.WriteLine("=== PART 2: ONE TRANSACTION, TWO REPOSITORIES ===\n");

        var (context, connection) = NewDatabase();
        await using var unitOfWork = new UnitOfWork(context);

        var customer = new Customer { Name = "Grace", Credit = 100m };
        await unitOfWork.Customers.AddAsync(customer);
        await unitOfWork.SaveChangesAsync();

        Console.WriteLine("Placing an order that spends more credit than exists:\n");

        customer.Credit -= 250m;
        await unitOfWork.Orders.AddAsync(new Order { CustomerId = customer.Id, Amount = 250m });

        if (customer.Credit < 0)
        {
            // Nothing has been saved, so there is nothing to undo. Two
            // repositories, one pending change set, one decision.
            Console.WriteLine("  insufficient credit -- abandoning the whole change set");
            Console.WriteLine($"  orders in database: {await context.Orders.CountAsync()}");
            Console.WriteLine($"  customer credit in database: {(await context.Customers.AsNoTracking().FirstAsync()).Credit:C}");
        }

        Console.WriteLine("\nIf each repository called SaveChanges itself, the order would");
        Console.WriteLine("already be committed and the credit would not be. That is the bug");
        Console.WriteLine("the unit of work exists to prevent.");

        connection.Dispose();
    }

    public static async Task Part3WhereGenericRepositoriesBreak()
    {
        Console.WriteLine("=== PART 3: THE LIMIT OF A GENERIC REPOSITORY ===\n");

        var (context, connection) = NewDatabase();
        await using var unitOfWork = new UnitOfWork(context);

        var customer = new Customer { Name = "Linus", Credit = 10_000m };
        await unitOfWork.Customers.AddAsync(customer);

        for (var i = 1; i <= 2000; i++)
        {
            await unitOfWork.Orders.AddAsync(new Order
            {
                CustomerId = customer.Id,
                Amount = i,
                Status = i % 3 == 0 ? "Pending" : "Shipped"
            });
        }

        await unitOfWork.SaveChangesAsync();

        // With only IRepository<T>.ListAsync you would load 2000 rows into
        // memory and sum them in C#.
        var all = await unitOfWork.Orders.ListAsync();
        var clientSide = all.Where(o => o.CustomerId == customer.Id).Sum(o => o.Amount);
        Console.WriteLine($"  generic ListAsync + client-side Sum: {clientSide:C} (loaded {all.Count} rows)");

        // The specific repository pushes it into SQL.
        var serverSide = await unitOfWork.Orders.GetTotalSpendAsync(customer.Id);
        Console.WriteLine($"  GetTotalSpendAsync (SUM in SQL):     {serverSide:C} (loaded 1 value)");

        var pending = await unitOfWork.Orders.GetPendingForCustomerAsync(customer.Id);
        Console.WriteLine($"  GetPendingForCustomerAsync:          {pending.Count} rows, filtered in SQL");

        Console.WriteLine("\nSame answer, very different cost. A repository that only exposes");
        Console.WriteLine("ListAsync pushes every query client-side -- which is the usual reason");
        Console.WriteLine("people conclude 'the repository pattern is slow'.");

        connection.Dispose();
    }

    public static async Task Part4IsItWorthIt()
    {
        Console.WriteLine("=== PART 4: WHEN NOT TO BOTHER ===\n");

        var (context, connection) = NewDatabase();

        Console.WriteLine("The honest counter-argument:\n");
        Console.WriteLine("  DbSet<T> is already a repository. DbContext is already a unit of");
        Console.WriteLine("  work -- it tracks changes and commits them together. Wrapping them");
        Console.WriteLine("  in interfaces that forward one-to-one adds a layer and no ability.\n");

        var direct = await context.Customers.CountAsync();
        Console.WriteLine($"  straight to the context: {direct} customer(s)\n");

        Console.WriteLine("Reasons that DO justify the wrapper:");
        Console.WriteLine("  - your Application layer must not reference EF Core (see module 03)");
        Console.WriteLine("  - you want to name queries in domain terms, not LINQ");
        Console.WriteLine("  - you genuinely intend to swap the data store");
        Console.WriteLine("  - you want to fake persistence in tests without a database\n");
        Console.WriteLine("Reasons that do not:");
        Console.WriteLine("  - 'it is the standard pattern'");

        connection.Dispose();
    }
}
