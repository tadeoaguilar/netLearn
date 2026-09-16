using CosmosQuerying.Domain;
using CosmosQuerying.Dtos;
using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Demos;

/// <summary>
/// Part 5: projecting a narrow field list to cut RU cost, not just payload
/// size.
/// </summary>
public static class Part5Projections
{
    public static async Task RunAsync(Container orders)
    {
        Console.WriteLine("=== Part 5: Projections -- Reducing RU Cost, Not Just Payload Size ===\n");

        var fullQuery = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
            .WithParameter("@status", "Delivered");
        var (fullResults, fullRu) = await RunQuery<Order>(orders, fullQuery);

        // Selecting only the fields the caller actually needs shrinks the
        // amount of data Cosmos has to read off the document and send back --
        // fewer RU charged for the SAME logical query, not just a smaller
        // response on the wire.
        var projectedQuery = new QueryDefinition(
                "SELECT c.id, c.customerId, c.totalAmount FROM c WHERE c.status = @status")
            .WithParameter("@status", "Delivered");
        var (projectedResults, projectedRu) = await RunQuery<OrderSummaryDto>(orders, projectedQuery);

        Console.WriteLine($"Full documents:       {fullResults.Count} results, {fullRu:0.00} RU");
        Console.WriteLine($"Projected (3 fields):  {projectedResults.Count} results, {projectedRu:0.00} RU");
        Console.WriteLine();
        Console.WriteLine("The same idea works from LINQ too: GetItemLinqQueryable<Order>()");
        Console.WriteLine("   .Select(o => new { o.Id, o.TotalAmount })");
        Console.WriteLine("translates to the same kind of narrowed SELECT list -- RequestCharge is");
        Console.WriteLine("the number to watch, not just the byte size of what came back.");
        Console.WriteLine();
    }

    private static async Task<(List<T> Results, double RequestCharge)> RunQuery<T>(Container orders, QueryDefinition query)
    {
        using var iterator = orders.GetItemQueryIterator<T>(query);
        var results = new List<T>();
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
