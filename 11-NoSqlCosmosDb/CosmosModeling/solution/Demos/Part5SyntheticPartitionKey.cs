using CosmosModeling.Domain;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmosModeling.Demos;

/// <summary>Part 5: a synthetic/composite partition key for a hypothetical high-volume customer.</summary>
public static class Part5SyntheticPartitionKey
{
    public static async Task RunAsync(Container highVolumeOrders)
    {
        Console.WriteLine("=== Part 5: A synthetic/composite partition key ===\n");

        Console.WriteLine("Scenario: one customer places thousands of orders a month. Partitioning purely");
        Console.WriteLine("by CustomerId (Part 2's design) would put ALL of that customer's orders, forever,");
        Console.WriteLine("in one logical partition -- a hard 20GB storage ceiling and a throughput ceiling");
        Console.WriteLine("shared by every request that partition ever serves. Combining CustomerId with a");
        Console.WriteLine("time bucket (year-month) into one synthetic key keeps each partition bounded.");
        Console.WriteLine();

        var customerId = "cust-highvolume-1";
        DateTimeOffset[] orderDates =
        [
            new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero),
        ];

        foreach (var orderDate in orderDates)
        {
            var partitionKey = HighVolumeOrder.BuildPartitionKey(customerId, orderDate);
            var order = new HighVolumeOrder
            {
                CustomerId = customerId,
                OrderDate = orderDate,
                PartitionKey = partitionKey,
                OrderLines = [new OrderLine { ProductId = "sku-1", ProductName = "Widget", Quantity = 1, UnitPrice = 9.99m }],
            };
            await highVolumeOrders.UpsertItemAsync(order, new PartitionKey(partitionKey));
        }

        // Querying "just September" is now a single-partition query -- Cosmos only has to
        // look inside the "cust-highvolume-1:2026-09" partition, not every order this
        // customer has ever placed across every month.
        var septemberKey = HighVolumeOrder.BuildPartitionKey(customerId, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        var query = highVolumeOrders
            .GetItemLinqQueryable<HighVolumeOrder>(requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(septemberKey) })
            .Where(o => o.PartitionKey == septemberKey);

        using var iterator = query.ToFeedIterator();
        var septemberOrders = await iterator.ReadNextAsync();
        Console.WriteLine($"Single-partition query for '{septemberKey}' found {septemberOrders.Count} order(s), {septemberOrders.RequestCharge} RU.");

        Console.WriteLine();
        Console.WriteLine("Cost of this design: 'give me ALL of this customer's orders, all time' can no");
        Console.WriteLine("longer be a single-partition query -- it now has to fan out across every month");
        Console.WriteLine("partition that customer has ever used (a cross-partition query), or the app has to");
        Console.WriteLine("maintain a separate index of which months to look in. That's the trade you make");
        Console.WriteLine("deliberately, and only for customers active enough to actually need it.");
        Console.WriteLine();
    }
}
