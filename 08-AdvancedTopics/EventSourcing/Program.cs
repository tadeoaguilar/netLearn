using EventSourcing.Demos;

switch (args.Length > 0 ? args[0].ToLowerInvariant() : "menu")
{
    case "1": await Demos.Part1EventsAreTheTruth(); break;
    case "2": await Demos.Part2TimeTravel(); break;
    case "3": await Demos.Part3Snapshots(); break;
    case "4": await Demos.Part4ProjectionsAnswerNewQuestions(); break;
    case "5": await Demos.Part5OptimisticConcurrency(); break;
    case "all":
        await Demos.Part1EventsAreTheTruth(); Separator();
        await Demos.Part2TimeTravel(); Separator();
        await Demos.Part3Snapshots(); Separator();
        await Demos.Part4ProjectionsAnswerNewQuestions(); Separator();
        await Demos.Part5OptimisticConcurrency();
        break;
    default:
        Console.WriteLine("Event Sourcing\n");
        Console.WriteLine("  dotnet run -- 1     Events are the truth");
        Console.WriteLine("  dotnet run -- 2     State at any point in the past");
        Console.WriteLine("  dotnet run -- 3     Snapshots");
        Console.WriteLine("  dotnet run -- 4     Projections answer new questions");
        Console.WriteLine("  dotnet run -- 5     Optimistic concurrency");
        Console.WriteLine("  dotnet run -- all   Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
