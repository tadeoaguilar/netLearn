using CosmosIndexingAndThroughput.Data;
using CosmosIndexingAndThroughput.Models;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Metrics;

// Every Cosmos response carries a RequestCharge (RUs consumed). This walks
// through four representative operations side by side so the relative cost
// of each shape of request is visible, not just asserted by documentation.
public static class RequestChargeDemo
{
    public static async Task RunAsync(Container orders, IReadOnlyList<Customer> customers)
    {
        Console.WriteLine("=== Part 3: Reading RequestCharge ===");

        var target = customers[0];
        var (found, _) = await orders.ExecuteAndMeasureAsync<Order>(
            new QueryDefinition("SELECT TOP 1 * FROM o WHERE o.customerId = @cid").WithParameter("@cid", target.Id),
            new QueryRequestOptions { PartitionKey = new PartitionKey(target.Id) });

        if (found.Count == 0)
        {
            Console.WriteLine("No orders found for this customer yet -- skipping the RequestCharge walkthrough.");
            return;
        }

        var order = found[0];

        var pointRead = await orders.ReadItemAsync<Order>(order.Id, new PartitionKey(order.CustomerId));
        Console.WriteLine($"Point read (ReadItemAsync by id + partition key):     {pointRead.RequestCharge,7:F2} RU");

        var singlePartitionQuery = new QueryDefinition("SELECT * FROM o WHERE o.customerId = @cid")
            .WithParameter("@cid", order.CustomerId);
        var (_, singlePartitionCharge) = await orders.ExecuteAndMeasureAsync<Order>(
            singlePartitionQuery, new QueryRequestOptions { PartitionKey = new PartitionKey(order.CustomerId) });
        Console.WriteLine($"Single-partition query (WHERE customerId = ...):     {singlePartitionCharge,7:F2} RU");

        var crossPartitionQuery = new QueryDefinition("SELECT * FROM o WHERE o.status = @status")
            .WithParameter("@status", "Shipped");
        var (crossResults, crossPartitionCharge) = await orders.ExecuteAndMeasureAsync<Order>(crossPartitionQuery);
        Console.WriteLine($"Cross-partition query (WHERE status = ..., fans out): {crossPartitionCharge,7:F2} RU ({crossResults.Count} rows)");

        var writeSample = SampleOrderFactory.Create(target.Id, "ru-demo-write");
        var writeResponse = await orders.UpsertItemAsync(writeSample, new PartitionKey(writeSample.CustomerId));
        Console.WriteLine($"Write (UpsertItemAsync):                             {writeResponse.RequestCharge,7:F2} RU");

        Console.WriteLine("The point read and single-partition query are the cheapest -- Cosmos knows exactly which");
        Console.WriteLine("physical partition to visit. The cross-partition query fans out to every partition and pays");
        Console.WriteLine("for it, even when it returns a similar number of rows.");
        Console.WriteLine();
    }
}
