using AdvancedDI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedDI.Demos;

public static class Part2Decorator
{
    public static void Run()
    {
        Console.WriteLine("=== PART 2: DECORATOR PATTERN ===\n");

        var services = new ServiceCollection();
        services.AddTransient<OrderService>();

        // Caching must be a singleton for its HashSet to survive between calls;
        // the chain around it is built once, here.
        services.AddSingleton<IOrderService>(provider =>
        {
            var baseService = provider.GetRequiredService<OrderService>();
            var withCaching = new CachingOrderServiceDecorator(baseService);
            var withValidation = new ValidationOrderServiceDecorator(withCaching);
            var withLogging = new LoggingOrderServiceDecorator(withValidation);
            return withLogging;
        });

        var provider = services.BuildServiceProvider();
        var orderService = provider.GetRequiredService<IOrderService>();

        Console.WriteLine("Order 1:");
        orderService.PlaceOrder("Laptop", 2);

        Console.WriteLine("\nOrder 2 (duplicate):");
        orderService.PlaceOrder("Laptop", 2);

        Console.WriteLine("\nOrder 3 (invalid - empty name):");
        orderService.PlaceOrder("", 5);

        Console.WriteLine("\nOrder 4 (invalid - zero quantity):");
        orderService.PlaceOrder("Monitor", 0);
    }
}
