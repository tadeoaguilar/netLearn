using CosmosModeling.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosModeling.Demos;

/// <summary>Part 3: embedding OrderLines vs. a hypothetical separate OrderLines container.</summary>
public static class Part3EmbeddingVsReferencing
{
    public static async Task RunAsync(Container orders)
    {
        Console.WriteLine("=== Part 3: Embedding vs. referencing ===\n");

        Console.WriteLine("CHOSEN: OrderLine[] embedded directly inside Order.");
        Console.WriteLine("  Order lines are always read and written together with their order and are");
        Console.WriteLine("  never queried on their own -- 'show me line item X in isolation' isn't a real");
        Console.WriteLine("  use case here. A single point read/write of the Order document is everything");
        Console.WriteLine("  any operation on an order's lines ever needs.");
        Console.WriteLine();
        Console.WriteLine("REJECTED: a separate 'orderlines' container, each line referencing its Order by");
        Console.WriteLine("  OrderId. Every screen that shows an order (which is every screen that touches");
        Console.WriteLine("  orders at all) would now need a SECOND round trip to fetch its lines -- and, if");
        Console.WriteLine("  that container weren't also partitioned by customerId, a cross-partition query");
        Console.WriteLine("  to boot. Strictly more RU cost and latency for data that's never useful alone.");
        Console.WriteLine();
        Console.WriteLine("WHEN YOU WOULD REFERENCE INSTEAD: a Product catalog, referenced from each");
        Console.WriteLine("  OrderLine by ProductId rather than embedded. Products differ from order lines");
        Console.WriteLine("  on every axis that made embedding right here: they're (a) large -- full");
        Console.WriteLine("  descriptions, images, specs -- so copying one into every order that ever sold");
        Console.WriteLine("  it bloats every Order document with data the order doesn't need; (b) SHARED");
        Console.WriteLine("  across thousands of orders, so embedding means a price-typo fix would have to");
        Console.WriteLine("  rewrite every historical order that ever referenced it (or, deliberately, NOT --");
        Console.WriteLine("  a price snapshot at time of sale is sometimes exactly what you want, which is");
        Console.WriteLine("  why this must be a conscious choice, not a default); and (c) independently");
        Console.WriteLine("  queried and updated -- browsing the catalog or changing stock levels never");
        Console.WriteLine("  needs to touch a single order.");
        Console.WriteLine();

        // Prove the embedded design's payoff: one ReadItemAsync returns the order AND every
        // one of its line items in the same response -- no second round trip, ever.
        var customerId = $"cust-{Guid.NewGuid():N}";
        var order = new Order
        {
            CustomerId = customerId,
            OrderLines = [new OrderLine { ProductId = "sku-9", ProductName = "Monitor", Quantity = 1, UnitPrice = 249.00m }],
        };
        await orders.UpsertItemAsync(order, new PartitionKey(customerId));

        var read = await orders.ReadItemAsync<Order>(order.Id, new PartitionKey(customerId));
        Console.WriteLine($"One round trip returned the order AND its {read.Resource.OrderLines.Count} line item(s), for {read.RequestCharge} RU.");
        Console.WriteLine();
    }
}
