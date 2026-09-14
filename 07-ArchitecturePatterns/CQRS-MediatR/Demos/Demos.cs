using CqrsMediatR.Behaviors;
using CqrsMediatR.Features;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsMediatR.Demos;

public static class Demos
{
    public static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<Catalogue>();
        services.AddSingleton<RequestLog>();

        // Handlers and validators are discovered, not listed. Adding a command
        // means adding a file -- nothing here changes.
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(Catalogue).Assembly));
        services.AddValidatorsFromAssembly(typeof(Catalogue).Assembly);

        // Order matters: logging wraps validation, so a rejected request is
        // still timed and logged.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services.BuildServiceProvider();
    }

    public static async Task Part1CommandsAndQueries()
    {
        Console.WriteLine("=== PART 1: COMMANDS AND QUERIES ===\n");

        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();
        var catalogue = provider.GetRequiredService<Catalogue>();

        await sender.Send(new AddProductCommand("SKU-1", "Mechanical keyboard", 149.99m, 25));
        await sender.Send(new AddProductCommand("SKU-2", "Ultrawide monitor", 699m, 4));

        Console.WriteLine("Commands sent. Now a query:\n");

        var view = await sender.Send(new GetProductQuery("SKU-2"));
        Console.WriteLine($"  {view!.Sku} {view.Name} {view.Price:C} -- {view.Availability}");

        Console.WriteLine($"\nwrites={catalogue.WriteCount} reads={catalogue.ReadCount}");
        Console.WriteLine("\nCommands returned an id or nothing. Queries returned a shape built");
        Console.WriteLine("for display. Neither handler knows the other exists.");
    }

    public static async Task Part2ValidationPipeline()
    {
        Console.WriteLine("=== PART 2: THE VALIDATION BEHAVIOUR ===\n");

        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();
        var catalogue = provider.GetRequiredService<Catalogue>();

        Console.WriteLine("Sending an invalid command:\n");

        try
        {
            await sender.Send(new AddProductCommand("nonsense", "", -5m, -1));
        }
        catch (ValidationException ex)
        {
            foreach (var failure in ex.Errors)
            {
                Console.WriteLine($"  {failure.PropertyName}: {failure.ErrorMessage}");
            }
        }

        Console.WriteLine($"\n  writes performed: {catalogue.WriteCount}");
        Console.WriteLine("\nAddProductHandler has no validation code in it at all. The");
        Console.WriteLine("behaviour rejected the command before the handler was reached.");
    }

    public static async Task Part3CrossCuttingForFree()
    {
        Console.WriteLine("=== PART 3: ONE BEHAVIOUR, EVERY REQUEST ===\n");

        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();
        var log = provider.GetRequiredService<RequestLog>();

        await sender.Send(new AddProductCommand("SKU-1", "Keyboard", 100m, 5));
        await sender.Send(new GetProductQuery("SKU-1"));
        await sender.Send(new ListProductsQuery());

        try { await sender.Send(new RepriceProductCommand("SKU-1", -1m)); }
        catch (ValidationException) { /* expected */ }

        Console.WriteLine("Pipeline log:\n");
        foreach (var entry in log.Entries)
        {
            Console.WriteLine($"  {entry}");
        }

        Console.WriteLine("\nFour different request types, one logging implementation, zero");
        Console.WriteLine("handlers aware of it. The failed one is logged as well.");
    }

    public static async Task Part4AddingAFeature()
    {
        Console.WriteLine("=== PART 4: WHAT ADDING A FEATURE COSTS ===\n");

        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new AddProductCommand("SKU-1", "Keyboard", 99m, 20));
        await sender.Send(new AddProductCommand("SKU-2", "Monitor", 699m, 2));
        await sender.Send(new AddProductCommand("SKU-3", "Mouse", 45m, 0));

        var cheap = await sender.Send(new ListProductsQuery(MaxPrice: 100m));

        Console.WriteLine("Products at or below $100:\n");
        foreach (var product in cheap)
        {
            Console.WriteLine($"  {product.Sku} {product.Name,-20} {product.Price,8:C}  {product.Availability}");
        }

        Console.WriteLine("\nListProductsQuery was added as one record plus one handler.");
        Console.WriteLine("No registration was edited, no interface grew a method, and every");
        Console.WriteLine("existing behaviour applied to it automatically.");
    }
}
