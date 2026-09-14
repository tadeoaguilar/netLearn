using EventDriven.Bus;
using EventDriven.Services;

namespace EventDriven.Demos;

public static class Demos
{
    public static async Task Part1Choreography()
    {
        Console.WriteLine("=== PART 1: CHOREOGRAPHY ===\n");

        var bus = new EventBus();
        var orders = new OrderService(bus);
        var payments = new PaymentService(bus);
        var shipping = new ShippingService(bus);

        await orders.PlaceOrderAsync("ORD-1", "cust-7", 129.99m);

        Console.WriteLine($"\nplaced={orders.Placed.Count} charged={payments.Charged.Count} shipped={shipping.Shipped.Count}");
        Console.WriteLine("\nOrderService called nothing. It announced a fact, and two other");
        Console.WriteLine("services reacted -- the second one to an event the first emitted.");
    }

    public static async Task Part2AddingASubscriber()
    {
        Console.WriteLine("=== PART 2: ADDING A SUBSCRIBER CHANGES NOTHING ===\n");

        var bus = new EventBus();
        var orders = new OrderService(bus);
        _ = new PaymentService(bus);
        _ = new ShippingService(bus);

        // Added afterwards. No existing service is aware of it.
        var analytics = new AnalyticsService(bus);

        await orders.PlaceOrderAsync("ORD-2", "cust-1", 50m);
        await orders.PlaceOrderAsync("ORD-3", "cust-2", 75m);

        Console.WriteLine($"\n[analytics] {analytics.OrderCount} orders, revenue {analytics.Revenue:C}");
        Console.WriteLine("\nAnalytics was bolted on with zero changes elsewhere. In a");
        Console.WriteLine("request/response design this would have meant editing OrderService.");
    }

    public static async Task Part3FailureIsolation()
    {
        Console.WriteLine("=== PART 3: ONE BAD SUBSCRIBER ===\n");

        var bus = new EventBus();
        var orders = new OrderService(bus);
        var payments = new PaymentService(bus);
        _ = new FlakyAuditService(bus);   // throws every time
        var analytics = new AnalyticsService(bus);

        await orders.PlaceOrderAsync("ORD-4", "cust-9", 42m);

        Console.WriteLine($"\ncharged={payments.Charged.Count} analytics={analytics.OrderCount}");
        Console.WriteLine("Audit threw, and payment and analytics still ran.");

        foreach (var entry in bus.Log.Where(l => l.Contains("failed")))
        {
            Console.WriteLine($"  {entry}");
        }

        Console.WriteLine("\nIn a real bus each subscriber gets its own queue and retry budget,");
        Console.WriteLine("so a broken consumer falls behind instead of taking others down.");
    }

    public static async Task Part4EventualConsistency()
    {
        Console.WriteLine("=== PART 4: EVENTUAL CONSISTENCY ===\n");

        var bus = new EventBus();
        var orders = new OrderService(bus);
        var payments = new PaymentService(bus);
        var shipping = new ShippingService(bus);

        Console.WriteLine("Placing an order and reading state IMMEDIATELY:\n");

        var placing = orders.PlaceOrderAsync("ORD-5", "cust-3", 99m);

        // Read before the flow has settled. This is the window every
        // event-driven system has, and where "I just paid, why does it say
        // pending?" bug reports come from.
        Console.WriteLine($"  right now: charged={payments.Charged.Count} shipped={shipping.Shipped.Count}");

        await placing;

        Console.WriteLine($"  settled:   charged={payments.Charged.Count} shipped={shipping.Shipped.Count}");
        Console.WriteLine("\nThe system was briefly inconsistent and then correct. Designs that");
        Console.WriteLine("cannot tolerate that window need a transaction, not an event.");
    }
}
