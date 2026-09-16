using CosmosQuerying.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Tests;

[Collection(CosmosCollection.Name)]
public class Part2SqlApiTests(CosmosFixture fixture)
{
    [Fact]
    public async Task Parameterized_Query_Returns_Only_Matching_Status()
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
            .WithParameter("@status", "Pending");
        var results = await ToListAsync<Order>(fixture.Orders, query);

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(o => o.Status == "Pending");
    }

    [Fact]
    public async Task Parameterized_Query_Returns_Empty_For_A_Status_That_Does_Not_Exist()
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
            .WithParameter("@status", "NotARealStatus");
        var results = await ToListAsync<Order>(fixture.Orders, query);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Injection_Shaped_Parameter_Value_Is_Treated_As_Data_Not_As_Sql()
    {
        // Looks like a classic SQL-injection payload, but WithParameter sends
        // it to Cosmos as the VALUE of @status, never as query text -- there
        // is no WHERE clause for it to "escape out of".
        var maliciousInput = "Pending' OR 1=1 --";
        var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
            .WithParameter("@status", maliciousInput);

        var results = await ToListAsync<Order>(fixture.Orders, query);

        // If the injection had "worked" this would return every order;
        // instead it returns none, because no order's status literally
        // equals the string "Pending' OR 1=1 --".
        results.Should().BeEmpty();

        var totalOrders = await CountAsync(fixture.Orders, "SELECT VALUE COUNT(1) FROM c");
        totalOrders.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Multiple_Parameters_Combine_With_And()
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status AND c.totalAmount > @minTotal")
            .WithParameter("@status", "Delivered")
            .WithParameter("@minTotal", 0m);

        var results = await ToListAsync<Order>(fixture.Orders, query);

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(o => o.Status == "Delivered" && o.TotalAmount > 0);
    }

    private static async Task<List<T>> ToListAsync<T>(Container container, QueryDefinition query)
    {
        using var iterator = container.GetItemQueryIterator<T>(query);
        var results = new List<T>();
        while (iterator.HasMoreResults)
        {
            results.AddRange(await iterator.ReadNextAsync());
        }

        return results;
    }

    private static async Task<int> CountAsync(Container container, string sql)
    {
        using var iterator = container.GetItemQueryIterator<int>(new QueryDefinition(sql));
        var total = 0;
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            total += page.FirstOrDefault();
        }

        return total;
    }
}
