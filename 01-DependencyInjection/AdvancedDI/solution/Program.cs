using AdvancedDI.Demos;

// Reference solution for the AdvancedDI exercise.
//
// Each part of EXERCISE.md runs on its own so you can compare one section at a
// time against your own work:
//
//     dotnet run -- 1     # Factory pattern
//     dotnet run -- 7     # Multi-tenant challenge
//     dotnet run -- all   # Everything, in order

var choice = args.Length > 0 ? args[0] : "menu";

switch (choice)
{
    case "1": Part1Factory.Run(); break;
    case "2": Part2Decorator.Run(); break;
    case "3": Part3Options.Run(); break;
    case "4": Part4KeyedServices.Run(); break;
    case "5": Part5Conditional.Run(args); break;
    case "6": Part6Scopes.Run(); break;
    case "7": Part7Challenge.Run(); break;

    case "all":
        Part1Factory.Run();
        Separator();
        Part2Decorator.Run();
        Separator();
        Part3Options.Run();
        Separator();
        Part4KeyedServices.Run();
        Separator();
        Part5Conditional.Run(args);
        Separator();
        Part6Scopes.Run();
        Separator();
        Part7Challenge.Run();
        break;

    default:
        Console.WriteLine("AdvancedDI -- reference solution\n");
        Console.WriteLine("  dotnet run -- 1     Factory pattern");
        Console.WriteLine("  dotnet run -- 2     Decorator pattern");
        Console.WriteLine("  dotnet run -- 3     Configuration binding (Options)");
        Console.WriteLine("  dotnet run -- 4     Keyed services");
        Console.WriteLine("  dotnet run -- 5     Conditional registration");
        Console.WriteLine("  dotnet run -- 6     Service provider scopes");
        Console.WriteLine("  dotnet run -- 7     Challenge: multi-tenant notifications");
        Console.WriteLine("  dotnet run -- all   Run every part in order");
        break;
}

static void Separator() => Console.WriteLine($"\n{new string('-', 60)}\n");
