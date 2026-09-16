using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Metrics;

// Cosmos reports RequestCharge per page, not per query -- this sums it
// across every page so callers get one honest RU total for the whole query.
public static class CosmosQueryExtensions
{
    public static async Task<(List<T> Items, double RequestCharge)> ExecuteAndMeasureAsync<T>(
        this Container container,
        QueryDefinition query,
        QueryRequestOptions? options = null)
    {
        var items = new List<T>();
        var totalCharge = 0.0;

        using var iterator = container.GetItemQueryIterator<T>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            totalCharge += page.RequestCharge;
            items.AddRange(page);
        }

        return (items, totalCharge);
    }
}
