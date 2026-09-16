using CosmosQuerying.Domain;
using CosmosQuerying.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmosQuerying.Demos;

/// <summary>
/// Part 4: paging with <see cref="FeedIterator{T}"/> and continuation
/// tokens, compared against LINQ's <c>Skip</c>/<c>Take</c>.
/// </summary>
public static class Part4Pagination
{
    public static async Task RunAsync(Container orders)
    {
        Console.WriteLine("=== Part 4: Pagination -- FeedIterator/Continuation Tokens vs. Skip/Take ===\n");

        const int pageSize = 20;
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.orderDate");

        // FeedIterator pages through the result set on Cosmos's terms: each
        // ReadNextAsync() call fetches roughly one page and returns a
        // ContinuationToken you can persist (in a "next page" API response,
        // say) and hand back later to resume exactly where you left off --
        // without Cosmos re-evaluating any of the documents from earlier
        // pages.
        using var iterator = orders.GetItemQueryIterator<Order>(
            query, requestOptions: new QueryRequestOptions { MaxItemCount = pageSize });

        var pageCount = 0;
        var totalItems = 0;
        double totalRu = 0;
        string? lastContinuationToken = null;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            pageCount++;
            totalItems += page.Count;
            totalRu += page.RequestCharge;
            lastContinuationToken = page.ContinuationToken;
        }

        Console.WriteLine($"FeedIterator: {pageCount} pages, {totalItems} total items, {totalRu:0.00} RU.");
        Console.WriteLine($"Final ContinuationToken is null (end of results): {lastContinuationToken is null}\n");

        // Skip/Take (OFFSET/LIMIT in the generated SQL) works, and reads
        // naturally -- but Cosmos still has to evaluate and discard every
        // skipped item server-side to get to your page. Paging deep into a
        // large result set this way gets steadily more expensive per page,
        // where a FeedIterator continuation token costs about the same
        // regardless of how many pages you've already read.
        const int skip = 100;
        const int take = 20;
        using var skipTakeIterator = orders.GetItemLinqQueryable<Order>(linqSerializerOptions: CosmosSerialization.LinqOptions)
            .OrderBy(o => o.Id)
            .Skip(skip)
            .Take(take)
            .ToFeedIterator();

        double skipTakeRu = 0;
        var skipTakeCount = 0;
        while (skipTakeIterator.HasMoreResults)
        {
            var page = await skipTakeIterator.ReadNextAsync();
            skipTakeRu += page.RequestCharge;
            skipTakeCount += page.Count;
        }

        Console.WriteLine($"Skip({skip}).Take({take}): {skipTakeCount} results, {skipTakeRu:0.00} RU for this one page alone.");
        Console.WriteLine("Every time you ask for this exact page with Skip/Take, Cosmos re-scans");
        Console.WriteLine("through the 100 skipped items again -- a saved continuation token would");
        Console.WriteLine("jump straight there instead. Skip/Take is fine for shallow pages (page 2,");
        Console.WriteLine("page 3); prefer continuation tokens for deep or unbounded paging.");
        Console.WriteLine();
    }
}
