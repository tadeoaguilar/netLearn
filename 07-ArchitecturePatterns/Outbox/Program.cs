using Outbox.Demos;

switch (args.Length > 0 ? args[0].ToLowerInvariant() : "menu")
{
    case "1": await Demos.Part1TheDualWriteProblem(); break;
    case "2": await Demos.Part2OutboxSurvivesTheCrash(); break;
    case "3": await Demos.Part3TheRelay(); break;
    case "4": await Demos.Part4BrokerDownThenRecovering(); break;
    case "5": await Demos.Part5AtLeastOnce(); break;
    case "all":
        await Demos.Part1TheDualWriteProblem(); Separator();
        await Demos.Part2OutboxSurvivesTheCrash(); Separator();
        await Demos.Part3TheRelay(); Separator();
        await Demos.Part4BrokerDownThenRecovering(); Separator();
        await Demos.Part5AtLeastOnce();
        break;
    default:
        Console.WriteLine("Outbox\n");
        Console.WriteLine("  dotnet run -- 1     The dual-write problem");
        Console.WriteLine("  dotnet run -- 2     One transaction, two writes");
        Console.WriteLine("  dotnet run -- 3     The relay");
        Console.WriteLine("  dotnet run -- 4     Surviving a broker outage");
        Console.WriteLine("  dotnet run -- 5     At-least-once delivery");
        Console.WriteLine("  dotnet run -- all   Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
