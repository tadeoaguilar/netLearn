using CqrsMediatR.Demos;

switch (args.Length > 0 ? args[0].ToLowerInvariant() : "menu")
{
    case "1": await Demos.Part1CommandsAndQueries(); break;
    case "2": await Demos.Part2ValidationPipeline(); break;
    case "3": await Demos.Part3CrossCuttingForFree(); break;
    case "4": await Demos.Part4AddingAFeature(); break;
    case "all":
        await Demos.Part1CommandsAndQueries(); Separator();
        await Demos.Part2ValidationPipeline(); Separator();
        await Demos.Part3CrossCuttingForFree(); Separator();
        await Demos.Part4AddingAFeature();
        break;
    default:
        Console.WriteLine("CQRS with MediatR\n");
        Console.WriteLine("  dotnet run -- 1     Commands and queries");
        Console.WriteLine("  dotnet run -- 2     The validation behaviour");
        Console.WriteLine("  dotnet run -- 3     One behaviour, every request");
        Console.WriteLine("  dotnet run -- 4     What adding a feature costs");
        Console.WriteLine("  dotnet run -- all   Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
