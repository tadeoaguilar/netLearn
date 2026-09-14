using Channels.Demos;

// Reference solution for the Channels exercise.
//
//     dotnet run -- 1           # basic producer/consumer
//     dotnet run -- challenge   # the message processing system
//     dotnet run -- all         # everything, in order

var choice = args.Length > 0 ? args[0].ToLowerInvariant() : "menu";

switch (choice)
{
    case "1": await Demos.Part1Basic(); break;
    case "2": await Demos.Part2Bounded(); break;
    case "3": await Demos.Part3MultipleProducersConsumers(); break;
    case "4": await Demos.Part4Pipeline(); break;
    case "5": await Demos.Part5CancellationAndErrors(); break;
    case "6": await Demos.Part6LogProcessor(); break;
    case "challenge": await Demos.ChallengeMessagePipeline(); break;

    case "all":
        await Demos.Part1Basic(); Separator();
        await Demos.Part2Bounded(); Separator();
        await Demos.Part3MultipleProducersConsumers(); Separator();
        await Demos.Part4Pipeline(); Separator();
        await Demos.Part5CancellationAndErrors(); Separator();
        await Demos.Part6LogProcessor(); Separator();
        await Demos.ChallengeMessagePipeline();
        break;

    default:
        Console.WriteLine("Channels -- reference solution\n");
        Console.WriteLine("  dotnet run -- 1           Basic producer-consumer");
        Console.WriteLine("  dotnet run -- 2           Bounded channels and backpressure");
        Console.WriteLine("  dotnet run -- 3           Multiple producers and consumers");
        Console.WriteLine("  dotnet run -- 4           Three-stage pipeline");
        Console.WriteLine("  dotnet run -- 5           Cancellation and errors");
        Console.WriteLine("  dotnet run -- 6           Background log processor");
        Console.WriteLine("  dotnet run -- challenge   Message processing system");
        Console.WriteLine("  dotnet run -- all         Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
