using Ddd.Demos;

switch (args.Length > 0 ? args[0].ToLowerInvariant() : "menu")
{
    case "1": await Demos.Part1ValueObjects(); break;
    case "2": await Demos.Part2Aggregates(); break;
    case "3": await Demos.Part3DomainEventsAndServices(); break;
    case "4": await Demos.Part4Specifications(); break;
    case "5": await Demos.Part5BoundedContexts(); break;
    case "all":
        await Demos.Part1ValueObjects(); Separator();
        await Demos.Part2Aggregates(); Separator();
        await Demos.Part3DomainEventsAndServices(); Separator();
        await Demos.Part4Specifications(); Separator();
        await Demos.Part5BoundedContexts();
        break;
    default:
        Console.WriteLine("Domain-Driven Design\n");
        Console.WriteLine("  dotnet run -- 1     Value objects");
        Console.WriteLine("  dotnet run -- 2     The aggregate boundary");
        Console.WriteLine("  dotnet run -- 3     Domain events and services");
        Console.WriteLine("  dotnet run -- 4     Specifications");
        Console.WriteLine("  dotnet run -- 5     Bounded contexts");
        Console.WriteLine("  dotnet run -- all   Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
