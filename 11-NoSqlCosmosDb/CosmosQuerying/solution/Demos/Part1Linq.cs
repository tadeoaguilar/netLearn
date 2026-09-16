using CosmosQuerying.Domain;
using CosmosQuerying.Dtos;
using CosmosQuerying.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmosQuerying.Demos;

/// <summary>
/// Part 1: the LINQ provider -- <c>GetItemLinqQueryable&lt;T&gt;()</c> looks
/// exactly like querying an in-memory collection or an EF Core DbSet, but
/// nothing here runs in .NET. Every clause below gets translated to a single
/// Cosmos SQL query string and shipped to the service; nothing is evaluated
/// client-side until you enumerate the FeedIterator.
/// </summary>
public static class Part1Linq
{
    public static async Task RunAsync(Container orders)
    {
        Console.WriteLine("=== Part 1: The LINQ Provider (GetItemLinqQueryable) ===\n");

        var query = orders.GetItemLinqQueryable<Order>(linqSerializerOptions: CosmosSerialization.LinqOptions)
            .Where(o => o.Status == "Shipped" && o.TotalAmount > 100)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new OrderSummaryDto { Id = o.Id, CustomerId = o.CustomerId, TotalAmount = o.TotalAmount });

        // ToQueryDefinition() (Microsoft.Azure.Cosmos.Linq) shows you the SQL
        // this LINQ expression actually became -- useful for building
        // intuition, and for confirming a query does what you think.
        Console.WriteLine($"Translated SQL: {query.ToQueryDefinition().QueryText}\n");

        using var iterator = query.ToFeedIterator();
        var results = new List<OrderSummaryDto>();
        double totalRu = 0;
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            totalRu += page.RequestCharge;
            results.AddRange(page);
        }

        Console.WriteLine($"Shipped orders over $100: {results.Count} results, {totalRu:0.00} RU total.\n");

        // What EF Core's LINQ provider gets you that Cosmos's doesn't: joins
        // across tables, most GroupBy shapes, and a far larger surface of
        // translatable string/date methods. Cosmos's LINQ provider only ever
        // produces single-CONTAINER SQL -- there's no equivalent of an
        // EF Core Include across containers, because Cosmos has no
        // server-side join between containers at all. Anything that looks
        // like "join orders to customers" is two round trips merged in your
        // own code, not one query.
        Console.WriteLine("Unlike EF Core's LINQ provider, Cosmos's LINQ provider never joins across");
        Console.WriteLine("containers and supports a narrower set of translatable operators -- when");
        Console.WriteLine("in doubt, check ToQueryDefinition() or drop to the SQL API (Part 2).");
        Console.WriteLine();
    }
}
