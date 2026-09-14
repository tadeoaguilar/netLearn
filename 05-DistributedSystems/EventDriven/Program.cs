using EventDriven.Demos;

switch (args.Length > 0 ? args[0].ToLowerInvariant() : "menu")
{
    case "1": await Demos.Part1Choreography(); break;
    case "2": await Demos.Part2AddingASubscriber(); break;
    case "3": await Demos.Part3FailureIsolation(); break;
    case "4": await Demos.Part4EventualConsistency(); break;
    case "all":
        await Demos.Part1Choreography(); Separator();
        await Demos.Part2AddingASubscriber(); Separator();
        await Demos.Part3FailureIsolation(); Separator();
        await Demos.Part4EventualConsistency();
        break;
    default:
        Console.WriteLine("EventDriven\n");
        Console.WriteLine("  dotnet run -- 1     Choreography");
        Console.WriteLine("  dotnet run -- 2     Adding a subscriber");
        Console.WriteLine("  dotnet run -- 3     Failure isolation");
        Console.WriteLine("  dotnet run -- 4     Eventual consistency");
        Console.WriteLine("  dotnet run -- all   Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
