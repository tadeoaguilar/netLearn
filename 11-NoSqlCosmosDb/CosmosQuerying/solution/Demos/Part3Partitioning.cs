using CosmosQuerying.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Demos;

/// <summary>
/// Part 3: cross-partition vs. partition-scoped queries. Same
/// <c>WHERE c.customerId = @customerId</c> query, run two ways.
/// </summary>
public static class Part3Partitioning
{
    public static async Task RunAsync(Container orders, string sampleCustomerId)
    {
        Console.WriteLine("=== Part 3: Cross-Partition vs. Partition-Scoped Queries ===\n");

        var query = new QueryDefinition("SELECT * FROM c WHERE c.customerId = @customerId")
            .WithParameter("@customerId", sampleCustomerId);

        // No PartitionKey in the request options: even though every matching
        // document happens to share one customerId, the SERVICE doesn't know
        // that from the query text alone unless you also tell it via
        // QueryRequestOptions.PartitionKey. Without it, Cosmos fans the query
        // out to every physical partition and merges the results -- costlier,
        // and it gets worse as the container grows more partitions.
        var (crossPartitionResults, crossPartitionRu) = await RunQuery(orders, query, partitionKey: null);

        // Scoping the SAME query to a single partition tells Cosmos exactly
        // where to look: one partition, no fan-out, no merge step.
        var (scopedResults, scopedRu) = await RunQuery(
            orders, query, partitionKey: new PartitionKey(sampleCustomerId));

        Console.WriteLine($"Cross-partition: {crossPartitionResults.Count} results, {crossPartitionRu:0.00} RU");
        Console.WriteLine($"Partition-scoped: {scopedResults.Count} results, {scopedRu:0.00} RU");
        Console.WriteLine();
        Console.WriteLine("When to use which: scope to a partition key whenever you know it up front");
        Console.WriteLine("(the overwhelmingly common case -- \"this customer's orders\"). Reach for");
        Console.WriteLine("a cross-partition query only when the query genuinely spans customers,");
        Console.WriteLine("e.g. an admin report over every order regardless of who placed it.");
        Console.WriteLine();
    }

    private static async Task<(List<Order> Results, double RequestCharge)> RunQuery(
        Container orders, QueryDefinition query, PartitionKey? partitionKey)
    {
        var options = partitionKey.HasValue ? new QueryRequestOptions { PartitionKey = partitionKey.Value } : null;
        using var iterator = orders.GetItemQueryIterator<Order>(query, requestOptions: options);

        var results = new List<Order>();
        double totalRu = 0;
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            totalRu += page.RequestCharge;
            results.AddRange(page);
        }

        return (results, totalRu);
    }
}
