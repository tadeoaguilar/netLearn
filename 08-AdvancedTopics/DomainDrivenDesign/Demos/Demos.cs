using Ddd.Ordering;
using Ddd.Shared;
using Ddd.Shipping;

namespace Ddd.Demos;

public static class Demos
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Order SampleOrder()
    {
        var order = Order.StartFor("cust-1");
        order.AddLine(new ProductId("KB-1"), "Mechanical keyboard", Money.Of(149.99m), 1);
        order.AddLine(new ProductId("MON-1"), "Ultrawide monitor", Money.Of(699m), 1);
        return order;
    }

    public static Task Part1ValueObjects()
    {
        Console.WriteLine("=== PART 1: VALUE OBJECTS ===\n");

        var five = Money.Of(5m);
        var alsoFive = Money.Of(5m);

        Console.WriteLine($"  {five} == {alsoFive}  ->  {five == alsoFive}");
        Console.WriteLine("  Equality is by VALUE. Two fivers are interchangeable.\n");

        try { Money.Of(-1m); }
        catch (DomainException ex) { Console.WriteLine($"  rejected: {ex.Message}"); }

        try { Money.Of(10m, "USD").Add(Money.Of(10m, "EUR")); }
        catch (DomainException ex) { Console.WriteLine($"  rejected: {ex.Message}"); }

        Console.WriteLine("\n  A plain `decimal` would have allowed both. The value object makes");
        Console.WriteLine("  an invalid state unrepresentable rather than merely discouraged.");
        return Task.CompletedTask;
    }

    public static Task Part2Aggregates()
    {
        Console.WriteLine("=== PART 2: THE AGGREGATE BOUNDARY ===\n");

        var order = SampleOrder();
        Console.WriteLine($"  lines: {order.Lines.Count}, total: {order.Total}");

        // Adding the same product again merges rather than duplicating -- a
        // rule that only something seeing ALL the lines can apply.
        order.AddLine(new ProductId("KB-1"), "Mechanical keyboard", Money.Of(149.99m), 2);
        Console.WriteLine($"  after re-adding KB-1: lines={order.Lines.Count}, total={order.Total}");

        Console.WriteLine("\n  Lines is IReadOnlyList, so this will not compile:");
        Console.WriteLine("      order.Lines.Add(...)   // no Add on IReadOnlyList\n");

        try { order.AddLine(new ProductId("X"), "In euros", Money.Of(10m, "EUR"), 1); }
        catch (DomainException ex) { Console.WriteLine($"  rejected: {ex.Message}"); }

        Console.WriteLine("\n  Every change goes through the root, so no caller can produce an");
        Console.WriteLine("  order the order itself considers invalid.");
        return Task.CompletedTask;
    }

    public static Task Part3DomainEventsAndServices()
    {
        Console.WriteLine("=== PART 3: DOMAIN EVENTS AND SERVICES ===\n");

        var order = SampleOrder();
        order.Place(Now);

        Console.WriteLine("  events recorded by the aggregate:");
        foreach (var @event in order.DomainEvents)
        {
            Console.WriteLine($"    {@event.GetType().Name} at {@event.OccurredAt:HH:mm}");
        }

        // Pricing spans an order AND a customer tier, so it belongs to neither.
        var calculator = new DiscountCalculator();

        foreach (var tier in new[] { "bronze", "silver", "gold" })
        {
            Console.WriteLine($"  {tier,-8} discount on {order.Total}: {calculator.CalculateDiscount(order, tier)}");
        }

        Console.WriteLine("\n  The aggregate RECORDS events; it never dispatches them. Dispatching");
        Console.WriteLine("  means knowing about a bus, and the domain must not.");
        return Task.CompletedTask;
    }

    public static Task Part4Specifications()
    {
        Console.WriteLine("=== PART 4: SPECIFICATIONS ===\n");

        var freeShipping = new QualifiesForFreeShipping(Money.Of(500m));
        var multipleItems = new HasMultipleItems();
        var both = new AndSpecification<Order>(freeShipping, multipleItems);

        var small = Order.StartFor("cust-1");
        small.AddLine(new ProductId("MOUSE"), "Mouse", Money.Of(45m), 1);

        var large = SampleOrder();

        foreach (var (name, order) in new[] { ("small", small), ("large", large) })
        {
            Console.WriteLine($"  {name} ({order.Total}):");
            Console.WriteLine($"    free shipping:  {freeShipping.IsSatisfiedBy(order)}");
            Console.WriteLine($"    multiple items: {multipleItems.IsSatisfiedBy(order)}");
            Console.WriteLine($"    both:           {both.IsSatisfiedBy(order)}");
        }

        Console.WriteLine("\n  `new QualifiesForFreeShipping(...)` says what the rule MEANS.");
        Console.WriteLine("  `o => o.Total.Amount > 500` says only what it does.");
        return Task.CompletedTask;
    }

    public static Task Part5BoundedContexts()
    {
        Console.WriteLine("=== PART 5: BOUNDED CONTEXTS ===\n");

        var order = SampleOrder();
        order.Place(Now);

        Console.WriteLine($"  Ordering.Order    -> total {order.Total}, {order.Lines.Count} lines");

        var weights = new Dictionary<string, decimal> { ["KB-1"] = 1.2m, ["MON-1"] = 7.5m };

        var shipment = OrderingToShippingTranslator.ToShipment(
            order,
            Address.Create("1 Main St", "Springfield", "12345", "US"),
            productId => weights[productId.Value]);

        Console.WriteLine($"  Shipping.Shipment -> {shipment.Weight.Kilograms}kg to {shipment.Destination.City}");
        Console.WriteLine($"                       references order {shipment.ExternalOrderReference[..8]}...");

        shipment.Dispatch("TRK-99");
        Console.WriteLine($"  dispatched: {shipment.Status}, tracking {shipment.TrackingNumber}");

        Console.WriteLine("\n  Two contexts, two models, one word. Shipping has no idea what");
        Console.WriteLine("  the order cost, and Ordering has no idea what it weighs. Neither");
        Console.WriteLine("  had to grow fields for the other's benefit.");
        Console.WriteLine("\n  They connect through a translator -- an anti-corruption layer --");
        Console.WriteLine("  and reference each other by identity, never by object reference.");
        return Task.CompletedTask;
    }
}
