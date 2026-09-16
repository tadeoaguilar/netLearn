using CosmosQuerying.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Tests;

[Collection(CosmosCollection.Name)]
public class Part3PartitioningTests(CosmosFixture fixture)
{
    [Fact]
    public async Task CrossPartition_And_PartitionScoped_Queries_Return_The_Same_Logical_Results()
    {
        var customerId = await GetACustomerIdWithOrders();

        var query = new QueryDefinition("SELECT * FROM c WHERE c.customerId = @customerId")
            .WithParameter("@customerId", customerId);

        var (crossPartitionResults, crossPartitionRu) = await RunQuery<Order>(query, partitionKey: null);
        var (scopedResults, scopedRu) = await RunQuery<Order>(query, new PartitionKey(customerId));

        crossPartitionResults.Should().NotBeEmpty();
        scopedResults.Select(o => o.Id).Should().BeEquivalentTo(crossPartitionResults.Select(o => o.Id));

        crossPartitionRu.Should().BeGreaterThan(0);
        scopedRu.Should().BeGreaterThan(0);
        // Scoping to a single partition should never cost MORE than fanning
        // out across every partition for the same logical query.
        scopedRu.Should().BeLessThanOrEqualTo(crossPartitionRu);
    }

    [Fact]
    public async Task PartitionScoped_Query_Only_Returns_Documents_From_That_Partition()
    {
        var customerId = await GetACustomerIdWithOrders();

        var query = new QueryDefinition("SELECT * FROM c WHERE c.customerId = @customerId")
            .WithParameter("@customerId", customerId);

        var (scopedResults, _) = await RunQuery<Order>(query, new PartitionKey(customerId));

        scopedResults.Should().NotBeEmpty();
        scopedResults.Should().OnlyContain(o => o.CustomerId == customerId);
    }

    [Fact]
    public async Task CrossPartition_Query_With_No_Filter_Returns_Every_Order()
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var (allResults, ru) = await RunQuery<Order>(query, partitionKey: null);

        var expectedCount = await CountAsync("SELECT VALUE COUNT(1) FROM c");

        allResults.Should().HaveCount(expectedCount);
        ru.Should().BeGreaterThan(0);
    }

    private async Task<string> GetACustomerIdWithOrders()
    {
        var query = new QueryDefinition("SELECT VALUE c.customerId FROM c OFFSET 0 LIMIT 1");
        using var iterator = fixture.Orders.GetItemQueryIterator<string>(query);
        var page = await iterator.ReadNextAsync();
        return page.First();
    }

    private async Task<(List<T> Results, double RequestCharge)> RunQuery<T>(QueryDefinition query, PartitionKey? partitionKey)
    {
        var options = partitionKey.HasValue ? new QueryRequestOptions { PartitionKey = partitionKey.Value } : null;
        using var iterator = fixture.Orders.GetItemQueryIterator<T>(query, requestOptions: options);

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

    private async Task<int> CountAsync(string sql)
    {
        using var iterator = fixture.Orders.GetItemQueryIterator<int>(new QueryDefinition(sql));
        var total = 0;
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            total += page.FirstOrDefault();
        }

        return total;
    }
}
