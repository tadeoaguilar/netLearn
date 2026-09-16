using CosmosQuerying.Domain;
using CosmosQuerying.Dtos;
using CosmosQuerying.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmosQuerying.Tests;

[Collection(CosmosCollection.Name)]
public class Part1LinqTests(CosmosFixture fixture)
{
    [Fact]
    public async Task LinqQueryable_Filters_And_Sorts_Like_The_Equivalent_Sql()
    {
        var linqResults = await ToListAsync(
            fixture.Orders.GetItemLinqQueryable<Order>(linqSerializerOptions: CosmosSerialization.LinqOptions)
                .Where(o => o.Status == "Shipped"));

        var sqlQuery = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
            .WithParameter("@status", "Shipped");
        var sqlResults = await ToListAsync<Order>(fixture.Orders, sqlQuery);

        linqResults.Should().NotBeEmpty();
        linqResults.Select(o => o.Id).Should().BeEquivalentTo(sqlResults.Select(o => o.Id));
        linqResults.Should().OnlyContain(o => o.Status == "Shipped");
    }

    [Fact]
    public async Task LinqQueryable_Projection_Returns_Only_Selected_Fields()
    {
        var projected = await ToListAsync(
            fixture.Orders.GetItemLinqQueryable<Order>(linqSerializerOptions: CosmosSerialization.LinqOptions)
                .Where(o => o.TotalAmount > 0)
                .Select(o => new OrderSummaryDto { Id = o.Id, CustomerId = o.CustomerId, TotalAmount = o.TotalAmount }));

        projected.Should().NotBeEmpty();
        projected.Should().OnlyContain(p => p.Id != "" && p.CustomerId != "" && p.TotalAmount > 0);
    }

    [Fact]
    public void LinqQueryable_Translates_To_A_Single_Container_Sql_Query()
    {
        var query = fixture.Orders.GetItemLinqQueryable<Order>(linqSerializerOptions: CosmosSerialization.LinqOptions)
            .Where(o => o.Status == "Delivered");

        // ToQueryDefinition() proves this LINQ expression became one SQL
        // query string -- there's no way for a LINQ query built this way to
        // secretly touch a second container the way an EF Core Include can
        // secretly touch a second table.
        var queryDefinition = query.ToQueryDefinition();

        queryDefinition.QueryText.Should().Contain("SELECT").And.Contain("WHERE");
    }

    private static async Task<List<T>> ToListAsync<T>(IQueryable<T> queryable)
    {
        using var iterator = queryable.ToFeedIterator();
        var results = new List<T>();
        while (iterator.HasMoreResults)
        {
            results.AddRange(await iterator.ReadNextAsync());
        }

        return results;
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
}
