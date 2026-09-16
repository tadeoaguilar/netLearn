using CosmosQuerying.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Demos;

/// <summary>
/// Part 2: the SQL API directly via <see cref="QueryDefinition"/>, always
/// parameterized with <c>WithParameter</c>. Same lesson EfCoreQuerying
/// (module 10) teaches for <c>FromSqlInterpolated</c> vs. <c>FromSqlRaw</c> +
/// concatenation, applied to Cosmos's own SQL dialect.
/// </summary>
public static class Part2SqlApi
{
    public static async Task RunAsync(Container orders)
    {
        Console.WriteLine("=== Part 2: The SQL API with QueryDefinition ===\n");

        var status = "Cancelled";

        // ALWAYS build Cosmos SQL with QueryDefinition + WithParameter, never
        // by interpolating or concatenating a value into the query text.
        // The value becomes a real query parameter (@status) before the
        // query ever reaches the service, so it can never restructure the
        // WHERE clause.
        var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
            .WithParameter("@status", status);

        using var iterator = orders.GetItemQueryIterator<Order>(query);
        var results = new List<Order>();
        double totalRu = 0;
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            totalRu += page.RequestCharge;
            results.AddRange(page);
        }

        Console.WriteLine($"Orders with status '{status}': {results.Count} results, {totalRu:0.00} RU.\n");

        Console.WriteLine("""
            NEVER do this:
                new QueryDefinition($"SELECT * FROM c WHERE c.status = '{status}'")
            A status value of  ' OR 1=1 --  would turn the WHERE clause into
            "true for every row" -- the same SQL injection shape as
            string-concatenated T-SQL, just against Cosmos's SQL dialect
            instead. WithParameter keeps the value as data, never as query
            structure -- see Part2SqlApiTests for a test that proves it.
            """);
        Console.WriteLine();
    }
}
