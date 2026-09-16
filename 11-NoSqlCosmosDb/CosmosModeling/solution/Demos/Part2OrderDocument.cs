using CosmosModeling.Domain;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmosModeling.Demos;

/// <summary>
/// Part 2: the Order document with embedded OrderLines, and choosing /customerId as its
/// partition key.
/// </summary>
public static class Part2OrderDocument
{
    public static async Task RunAsync(Container orders)
    {
        Console.WriteLine("=== Part 2: The Order document, embedded OrderLines (partition key /customerId) ===\n");

        var customerId = $"cust-{Guid.NewGuid():N}";
        var order = new Order
        {
            CustomerId = customerId,
            Status = "Placed",
            OrderLines =
            [
                new OrderLine { ProductId = "sku-1", ProductName = "Keyboard", Quantity = 1, UnitPrice = 79.99m },
                new OrderLine { ProductId = "sku-2", ProductName = "Mouse", Quantity = 2, UnitPrice = 24.99m },
            ],
        };

        await orders.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        // "Give me this customer's orders" is this domain's most common order query. Every
        // order this customer has ever placed shares the same partition key, so scoping the
        // query with PartitionKey turns it into a single-partition query: Cosmos looks in one
        // place instead of fanning out across the whole container.
        var query = orders
            .GetItemLinqQueryable<Order>(requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(customerId) })
            .Where(o => o.CustomerId == customerId);

        using var iterator = query.ToFeedIterator();
        var page = await iterator.ReadNextAsync();
        Console.WriteLine($"Single-partition query found {page.Count} order(s) for {customerId} ({page.RequestCharge} RU).");

        Console.WriteLine();
        Console.WriteLine("Trade-off: partitioning by customerId keeps one customer's order history cheap");
        Console.WriteLine("to query together, but every order that customer EVER places lands in that same");
        Console.WriteLine("logical partition -- which has a 20GB storage ceiling and a throughput ceiling");
        Console.WriteLine("shared by every request against it. Fine for ordinary customers; not fine for one");
        Console.WriteLine("extremely high-volume account. Part 5 shows a synthetic key that bounds that case.");
        Console.WriteLine();
    }
}
