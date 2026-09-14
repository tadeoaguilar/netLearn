using AsyncAwait.Demos;

// Reference solution for the AsyncAwait exercise.
//
//     dotnet run -- 1           # sync vs async
//     dotnet run -- challenge   # the download manager
//     dotnet run -- all         # everything, in order
//
// Parts 1-3 and 7 use real delays, so they take a few seconds on purpose --
// the elapsed time IS the lesson.

var choice = args.Length > 0 ? args[0].ToLowerInvariant() : "menu";

switch (choice)
{
    case "1": await Demos.Part1SyncVsAsync(); break;
    case "2": await Demos.Part2Combinators(); break;
    case "3": await Demos.Part3Cancellation(); break;
    case "4": await Demos.Part4Exceptions(); break;
    case "5": await Demos.Part5ConfigureAwait(); break;
    case "6": await Demos.Part6Pitfalls(); break;
    case "7": await Demos.Part7DataProcessor(); break;
    case "challenge": await Demos.ChallengeDownloadManager(); break;

    case "all":
        await Demos.Part1SyncVsAsync(); Separator();
        await Demos.Part2Combinators(); Separator();
        await Demos.Part3Cancellation(); Separator();
        await Demos.Part4Exceptions(); Separator();
        await Demos.Part5ConfigureAwait(); Separator();
        await Demos.Part6Pitfalls(); Separator();
        await Demos.Part7DataProcessor(); Separator();
        await Demos.ChallengeDownloadManager();
        break;

    default:
        Console.WriteLine("AsyncAwait -- reference solution\n");
        Console.WriteLine("  dotnet run -- 1           Sync vs async");
        Console.WriteLine("  dotnet run -- 2           Task.WhenAll / Task.WhenAny");
        Console.WriteLine("  dotnet run -- 3           Cancellation tokens");
        Console.WriteLine("  dotnet run -- 4           Exception handling");
        Console.WriteLine("  dotnet run -- 5           ConfigureAwait");
        Console.WriteLine("  dotnet run -- 6           Common pitfalls");
        Console.WriteLine("  dotnet run -- 7           Async data processor");
        Console.WriteLine("  dotnet run -- challenge   Async download manager");
        Console.WriteLine("  dotnet run -- all         Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
