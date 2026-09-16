using CosmosQuerying.Domain;
using CosmosQuerying.Dtos;
using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Tests;

[Collection(CosmosCollection.Name)]
public class Part5ProjectionsTests(CosmosFixture fixture)
{
    [Fact]
    public async Task Projected_Query_Returns_The_Same_Ids_As_The_Full_Query()
    {
        var fullQuery = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
            .WithParameter("@status", "Delivered");
        var projectedQuery = new QueryDefinition("SELECT c.id, c.customerId, c.totalAmount FROM c WHERE c.status = @status")
            .WithParameter("@status", "Delivered");

        var (fullResults, _) = await RunQuery<Order>(fullQuery);
        var (projectedResults, _) = await RunQuery<OrderSummaryDto>(projectedQuery);

        projectedResults.Select(o => o.Id).Should().BeEquivalentTo(fullResults.Select(o => o.Id));
    }

    [Fact]
    public async Task Projected_Query_Request_Charge_Is_Lower_Or_Equal_To_The_Full_Query()
    {
        var fullQuery = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
            .WithParameter("@status", "Delivered");
        var projectedQuery = new QueryDefinition("SELECT c.id, c.customerId, c.totalAmount FROM c WHERE c.status = @status")
            .WithParameter("@status", "Delivered");

        var (_, fullRu) = await RunQuery<Order>(fullQuery);
        var (_, projectedRu) = await RunQuery<OrderSummaryDto>(projectedQuery);

        fullRu.Should().BeGreaterThan(0);
        projectedRu.Should().BeGreaterThan(0);
        projectedRu.Should().BeLessThanOrEqualTo(fullRu);
    }

    [Fact]
    public async Task Projected_Dto_Has_No_Property_For_Fields_That_Were_Never_Selected()
    {
        var projectedQuery = new QueryDefinition("SELECT c.id, c.customerId, c.totalAmount FROM c");
        var (results, _) = await RunQuery<OrderSummaryDto>(projectedQuery);

        results.Should().NotBeEmpty();
        // OrderSummaryDto has no Lines/Status/OrderDate property at all --
        // there is nothing to check on the object at runtime, which is the
        // point: that data never crossed the network in the first place.
        typeof(OrderSummaryDto).GetProperty("Lines").Should().BeNull();
        typeof(OrderSummaryDto).GetProperty("Status").Should().BeNull();
    }

    [Fact]
    public async Task Full_Document_Read_Includes_Order_Lines()
    {
        var fullQuery = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
            .WithParameter("@status", "Delivered");
        var (results, _) = await RunQuery<Order>(fullQuery);

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(o => o.Lines.Count > 0);
    }

    private async Task<(List<T> Results, double RequestCharge)> RunQuery<T>(QueryDefinition query)
    {
        using var iterator = fixture.Orders.GetItemQueryIterator<T>(query);
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
