using MessageQueues.Demos;

// Message queues, demonstrated in-process.
//
//     dotnet run -- 1    publish and consume
//     dotnet run -- 2    retry and dead-lettering
//     dotnet run -- 3    idempotent consumers
//     dotnet run -- 4    competing consumers
//     dotnet run -- all

switch (args.Length > 0 ? args[0].ToLowerInvariant() : "menu")
{
    case "1": await Demos.Part1BasicQueue(); break;
    case "2": await Demos.Part2RetryAndDeadLetter(); break;
    case "3": await Demos.Part3Idempotency(); break;
    case "4": await Demos.Part4CompetingConsumers(); break;
    case "all":
        await Demos.Part1BasicQueue(); Separator();
        await Demos.Part2RetryAndDeadLetter(); Separator();
        await Demos.Part3Idempotency(); Separator();
        await Demos.Part4CompetingConsumers();
        break;
    default:
        Console.WriteLine("MessageQueues\n");
        Console.WriteLine("  dotnet run -- 1     Publish and consume");
        Console.WriteLine("  dotnet run -- 2     Retry and dead-lettering");
        Console.WriteLine("  dotnet run -- 3     Idempotent consumers");
        Console.WriteLine("  dotnet run -- 4     Competing consumers");
        Console.WriteLine("  dotnet run -- all   Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
