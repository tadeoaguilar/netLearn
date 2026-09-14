using Resilience.Demos;

switch (args.Length > 0 ? args[0].ToLowerInvariant() : "menu")
{
    case "1": await Demos.Part1Retry(); break;
    case "2": await Demos.Part2RetryCannotFixEverything(); break;
    case "3": await Demos.Part3CircuitBreaker(); break;
    case "4": await Demos.Part4Timeout(); break;
    case "5": await Demos.Part5Fallback(); break;
    case "6": await Demos.Part6TheFullPipeline(); break;
    case "all":
        await Demos.Part1Retry(); Separator();
        await Demos.Part2RetryCannotFixEverything(); Separator();
        await Demos.Part3CircuitBreaker(); Separator();
        await Demos.Part4Timeout(); Separator();
        await Demos.Part5Fallback(); Separator();
        await Demos.Part6TheFullPipeline();
        break;
    default:
        Console.WriteLine("Resilience (Polly)\n");
        Console.WriteLine("  dotnet run -- 1     Retry");
        Console.WriteLine("  dotnet run -- 2     When retry makes things worse");
        Console.WriteLine("  dotnet run -- 3     Circuit breaker");
        Console.WriteLine("  dotnet run -- 4     Timeout");
        Console.WriteLine("  dotnet run -- 5     Fallback");
        Console.WriteLine("  dotnet run -- 6     Composing strategies");
        Console.WriteLine("  dotnet run -- all   Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
