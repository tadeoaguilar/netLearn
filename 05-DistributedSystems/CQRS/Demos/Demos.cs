using CQRS.Read;
using CQRS.Write;

namespace CQRS.Demos;

public static class Demos
{
    private static (WriteStore Write, ReadStore Read) Build()
    {
        var write = new WriteStore();
        var read = new ReadStore();

        read.Seed("SKU-1", "Mechanical keyboard");
        read.Seed("SKU-2", "Ultrawide monitor");

        write.Add(new Product("SKU-1", "Mechanical keyboard", 149.99m, 25));
        write.Add(new Product("SKU-2", "Ultrawide monitor", 699.00m, 4));

        return (write, read);
    }

    private static void Sync(WriteStore write, ReadStore read)
    {
        foreach (var @event in write.DrainEvents()) read.Project(@event);
    }

    public static Task Part1SeparateModels()
    {
        Console.WriteLine("=== PART 1: TWO MODELS, ONE TRUTH ===\n");

        var (write, read) = Build();
        Sync(write, read);

        Console.WriteLine("Write model (normalised, enforces rules):");
        var product = write.Get("SKU-2");
        Console.WriteLine($"  {product.Sku} price={product.Price} stock={product.Stock}");

        Console.WriteLine("\nRead model (denormalised, ready to render):");
        foreach (var item in read.All())
        {
            Console.WriteLine($"  {item.Sku} {item.Name,-22} {item.Price,8:C}  {item.Availability}");
        }

        Console.WriteLine("\n'Availability' does not exist on the write side. It is computed");
        Console.WriteLine("once when stock changes, not on every read.");
        return Task.CompletedTask;
    }

    public static Task Part2WritesFlowToReads()
    {
        Console.WriteLine("=== PART 2: PROJECTIONS ===\n");

        var (write, read) = Build();
        Sync(write, read);

        Console.WriteLine("Reserving 3 of SKU-2 (stock 4 -> 1):\n");
        write.Reserve("SKU-2", 3);
        Sync(write, read);

        var item = read.Get("SKU-2")!;
        Console.WriteLine($"  read model: stock={item.Stock} availability={item.Availability}");

        Console.WriteLine("\nRepricing SKU-1:\n");
        write.Reprice("SKU-1", 129.99m);
        Sync(write, read);

        Console.WriteLine($"  read model: {read.Get("SKU-1")!.Price:C}");
        Console.WriteLine($"\nEvents projected so far: {read.ProjectedEvents}");
        return Task.CompletedTask;
    }

    public static Task Part3ReadModelLag()
    {
        Console.WriteLine("=== PART 3: THE LAG IS REAL ===\n");

        var (write, read) = Build();
        Sync(write, read);

        Console.WriteLine($"before:      write stock={write.Get("SKU-1").Stock}  read stock={read.Get("SKU-1")!.Stock}");

        write.Reserve("SKU-1", 20);

        // Deliberately NOT synced. This is the window between a write
        // committing and its projection catching up.
        Console.WriteLine($"after write: write stock={write.Get("SKU-1").Stock}  read stock={read.Get("SKU-1")!.Stock}  <-- stale");

        Sync(write, read);
        Console.WriteLine($"after sync:  write stock={write.Get("SKU-1").Stock}  read stock={read.Get("SKU-1")!.Stock}");

        Console.WriteLine("\nThat middle line is the cost of CQRS. A user who writes and");
        Console.WriteLine("immediately reads can see their own change missing.");
        Console.WriteLine("Mitigations: read your own writes from the write model, show a");
        Console.WriteLine("pending state, or do not use CQRS for that screen.");
        return Task.CompletedTask;
    }

    public static Task Part4RulesLiveOnTheWriteSide()
    {
        Console.WriteLine("=== PART 4: RULES BELONG TO THE WRITE MODEL ===\n");

        var (write, read) = Build();
        Sync(write, read);

        try
        {
            write.Reserve("SKU-2", 999);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  rejected: {ex.Message}");
        }

        Sync(write, read);
        Console.WriteLine($"  read model unchanged: stock={read.Get("SKU-2")!.Stock}");

        Console.WriteLine("\nA rejected command emits no event, so the read model never sees");
        Console.WriteLine("it. Validation on the read side would be both too late and too weak --");
        Console.WriteLine("the read model is a cache, not a source of truth.");
        return Task.CompletedTask;
    }
}
