using TaskParallelLibrary.Demos;

// Reference solution for the TaskParallelLibrary exercise.
//
//     dotnet run -c Release -- 1           # Parallel.For / ForEach
//     dotnet run -c Release -- challenge   # Parallel image processor
//     dotnet run -c Release -- all         # Everything, in order
//
// Use -c Release for anything with a timing claim in it. Debug builds skip
// most JIT optimization, and the numbers you get are not worth reading.

var choice = args.Length > 0 ? args[0].ToLowerInvariant() : "menu";

switch (choice)
{
    case "1": Demos.Part1BasicParallel(); break;
    case "2": Demos.Part2Plinq(); break;
    case "3": Demos.Part3Control(); break;
    case "4": Demos.Part4Exceptions(); break;
    case "5": Demos.Part5Cancellation(); break;
    case "6": Demos.Part6Partitioning(); break;
    case "7": await Demos.Part7AsyncVsParallel(); break;
    case "challenge": Demos.ChallengeImageProcessor(); break;

    case "all":
        Demos.Part1BasicParallel(); Separator();
        Demos.Part2Plinq(); Separator();
        Demos.Part3Control(); Separator();
        Demos.Part4Exceptions(); Separator();
        Demos.Part5Cancellation(); Separator();
        Demos.Part6Partitioning(); Separator();
        await Demos.Part7AsyncVsParallel(); Separator();
        Demos.ChallengeImageProcessor();
        break;

    default:
        Console.WriteLine("TaskParallelLibrary -- reference solution\n");
        Console.WriteLine("  dotnet run -c Release -- 1           Parallel.For and Parallel.ForEach");
        Console.WriteLine("  dotnet run -c Release -- 2           PLINQ");
        Console.WriteLine("  dotnet run -c Release -- 3           Controlling parallelism");
        Console.WriteLine("  dotnet run -c Release -- 4           Exception handling");
        Console.WriteLine("  dotnet run -c Release -- 5           Cancellation");
        Console.WriteLine("  dotnet run -c Release -- 6           Partitioning strategies");
        Console.WriteLine("  dotnet run -c Release -- 7           Async vs parallel");
        Console.WriteLine("  dotnet run -c Release -- challenge   Parallel image processor");
        Console.WriteLine("  dotnet run -c Release -- all         Everything in order");
        Console.WriteLine($"\nThis machine has {Environment.ProcessorCount} logical cores.");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
