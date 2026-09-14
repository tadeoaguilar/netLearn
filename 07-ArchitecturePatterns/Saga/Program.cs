using Saga.Demos;

switch (args.Length > 0 ? args[0].ToLowerInvariant() : "menu")
{
    case "1": await Demos.Part1HappyPath(); break;
    case "2": await Demos.Part2CompensationOnFailure(); break;
    case "3": await Demos.Part3FailureInTheMiddle(); break;
    case "4": await Demos.Part4NothingToCompensate(); break;
    case "all":
        await Demos.Part1HappyPath(); Separator();
        await Demos.Part2CompensationOnFailure(); Separator();
        await Demos.Part3FailureInTheMiddle(); Separator();
        await Demos.Part4NothingToCompensate();
        break;
    default:
        Console.WriteLine("Saga\n");
        Console.WriteLine("  dotnet run -- 1     Happy path");
        Console.WriteLine("  dotnet run -- 2     Compensation on failure");
        Console.WriteLine("  dotnet run -- 3     Failure in the middle");
        Console.WriteLine("  dotnet run -- 4     Failure with nothing to undo");
        Console.WriteLine("  dotnet run -- all   Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
