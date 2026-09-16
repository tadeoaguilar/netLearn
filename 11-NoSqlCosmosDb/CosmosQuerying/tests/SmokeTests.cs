using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Tests;

[Collection(CosmosCollection.Name)]
public class SmokeTests(CosmosFixture fixture)
{
    [Fact]
    public async Task Seed_Produces_The_Expected_Number_Of_Customers()
    {
        var count = await CountAsync(fixture.Customers);
        count.Should().BeInRange(20, 30);
    }

    [Fact]
    public async Task Seed_Produces_Orders_In_The_Expected_Range()
    {
        var count = await CountAsync(fixture.Orders);
        count.Should().BeInRange(150, 200);
    }

    private static async Task<int> CountAsync(Container container)
    {
        using var iterator = container.GetItemQueryIterator<int>(new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));
        var total = 0;
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            total += page.FirstOrDefault();
        }

        return total;
    }
}
