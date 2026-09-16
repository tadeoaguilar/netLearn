using System.Net;
using CosmosIndexingAndThroughput.Data;
using CosmosIndexingAndThroughput.Metrics;
using CosmosIndexingAndThroughput.Models;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Indexing;

public static class IndexingWalkthrough
{
    public static async Task RunPart1DefaultIndexingAsync(Container orders, IReadOnlyList<Customer> customers)
    {
        Console.WriteLine("=== Part 1: Default indexing ===");

        var sample = SampleOrderFactory.Create(customers[0].Id, "default-policy-sample");
        var writeResponse = await orders.UpsertItemAsync(sample, new PartitionKey(sample.CustomerId));
        Console.WriteLine($"Write under the DEFAULT policy (every property, incl. orderLines, indexed): {writeResponse.RequestCharge:F2} RU");

        var byCustomer = new QueryDefinition("SELECT * FROM o WHERE o.customerId = @customerId")
            .WithParameter("@customerId", sample.CustomerId);
        var (_, customerCharge) = await orders.ExecuteAndMeasureAsync<Order>(
            byCustomer, new QueryRequestOptions { PartitionKey = new PartitionKey(sample.CustomerId) });
        Console.WriteLine($"Query filtering on the indexed top-level property customerId: {customerCharge:F2} RU");

        var byProduct = new QueryDefinition(
                "SELECT VALUE o FROM o JOIN l IN o.orderLines WHERE l.productId = @productId")
            .WithParameter("@productId", sample.OrderLines[0].ProductId);
        var (productMatches, productCharge) = await orders.ExecuteAndMeasureAsync<Order>(
            byProduct, new QueryRequestOptions { PartitionKey = new PartitionKey(sample.CustomerId) });
        Console.WriteLine($"Query reaching into orderLines (indexed by default too): {productCharge:F2} RU, {productMatches.Count} match(es)");

        Console.WriteLine("Every property was indexed automatically, including every element of orderLines.");
        Console.WriteLine("That makes both queries above fast -- but the write above also paid to maintain an index term");
        Console.WriteLine("for every orderLines element, on every single order write, even though this app never filters");
        Console.WriteLine("on orderLines directly in normal operation.");
        Console.WriteLine();
    }

    public static async Task RunPart2CustomIndexingPolicyAsync(Database database, Container orders, IReadOnlyList<Customer> customers)
    {
        Console.WriteLine("=== Part 2: Customizing the indexing policy ===");

        var orderByQuery = new QueryDefinition("SELECT * FROM o ORDER BY o.customerId ASC, o.orderDate DESC");

        Console.WriteLine("Attempting a two-property ORDER BY before any composite index exists...");
        try
        {
            await orders.ExecuteAndMeasureAsync<Order>(orderByQuery);
            Console.WriteLine("(Unexpectedly succeeded -- a composite index may already exist from a previous run.)");
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            var firstLine = ex.Message.Split('\n')[0];
            Console.WriteLine($"Failed as expected ({ex.StatusCode}): {firstLine}");
        }

        var properties = (await orders.ReadContainerAsync()).Resource;
        properties.IndexingPolicy = IndexingPolicyFactory.CreateOrdersIndexingPolicy();
        await orders.ReplaceContainerAsync(properties);
        Console.WriteLine("Replaced the indexing policy: excluded /orderLines/* and added a (customerId ASC, orderDate DESC) composite index.");

        var sample = SampleOrderFactory.Create(customers[0].Id, "custom-policy-sample");
        var writeResponse = await orders.UpsertItemAsync(sample, new PartitionKey(sample.CustomerId));
        Console.WriteLine($"Write of the SAME shape order under the CUSTOM policy: {writeResponse.RequestCharge:F2} RU");

        var byProduct = new QueryDefinition(
                "SELECT VALUE o FROM o JOIN l IN o.orderLines WHERE l.productId = @productId")
            .WithParameter("@productId", sample.OrderLines[0].ProductId);
        var (productMatches, productCharge) = await orders.ExecuteAndMeasureAsync<Order>(
            byProduct, new QueryRequestOptions { PartitionKey = new PartitionKey(sample.CustomerId) });
        Console.WriteLine($"Same orderLines query, now against an EXCLUDED path: {productCharge:F2} RU, {productMatches.Count} match(es)");
        Console.WriteLine("Cosmos can still find it -- paths don't have to be indexed to be queryable -- it just falls back");
        Console.WriteLine("to scanning within the partition instead of seeking through an index.");

        var (sorted, orderByCharge) = await orders.ExecuteAndMeasureAsync<Order>(orderByQuery);
        Console.WriteLine($"Two-property ORDER BY now succeeds: {orderByCharge:F2} RU, {sorted.Count} row(s)");
        Console.WriteLine();
    }
}
