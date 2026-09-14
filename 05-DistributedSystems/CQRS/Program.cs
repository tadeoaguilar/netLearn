using CQRS.Demos;

switch (args.Length > 0 ? args[0].ToLowerInvariant() : "menu")
{
    case "1": await Demos.Part1SeparateModels(); break;
    case "2": await Demos.Part2WritesFlowToReads(); break;
    case "3": await Demos.Part3ReadModelLag(); break;
    case "4": await Demos.Part4RulesLiveOnTheWriteSide(); break;
    case "all":
        await Demos.Part1SeparateModels(); Separator();
        await Demos.Part2WritesFlowToReads(); Separator();
        await Demos.Part3ReadModelLag(); Separator();
        await Demos.Part4RulesLiveOnTheWriteSide();
        break;
    default:
        Console.WriteLine("CQRS\n");
        Console.WriteLine("  dotnet run -- 1     Separate read and write models");
        Console.WriteLine("  dotnet run -- 2     Projections");
        Console.WriteLine("  dotnet run -- 3     Read model lag");
        Console.WriteLine("  dotnet run -- 4     Where the rules live");
        Console.WriteLine("  dotnet run -- all   Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
