using RepositoryUnitOfWork.Demos;

switch (args.Length > 0 ? args[0].ToLowerInvariant() : "menu")
{
    case "1": await Demos.Part1Repository(); break;
    case "2": await Demos.Part2UnitOfWorkAtomicity(); break;
    case "3": await Demos.Part3WhereGenericRepositoriesBreak(); break;
    case "4": await Demos.Part4IsItWorthIt(); break;
    case "all":
        await Demos.Part1Repository(); Separator();
        await Demos.Part2UnitOfWorkAtomicity(); Separator();
        await Demos.Part3WhereGenericRepositoriesBreak(); Separator();
        await Demos.Part4IsItWorthIt();
        break;
    default:
        Console.WriteLine("Repository / Unit of Work\n");
        Console.WriteLine("  dotnet run -- 1     The repository");
        Console.WriteLine("  dotnet run -- 2     One transaction, two repositories");
        Console.WriteLine("  dotnet run -- 3     Where generic repositories break");
        Console.WriteLine("  dotnet run -- 4     When not to bother");
        Console.WriteLine("  dotnet run -- all   Everything in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
