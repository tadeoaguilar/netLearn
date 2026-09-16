using CosmosModeling.Domain;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmosModeling.Tests;

[Collection(CosmosModelingCollection.Name)]
public class Part5SyntheticPartitionKeyTests(CosmosModelingFixture fixture)
{
    [Fact]
    public async Task HighVolumeOrders_Container_Has_PartitionKeyPath_PartitionKey()
    {
        var response = await fixture.HighVolumeOrdersContainer.ReadContainerAsync();

        response.Resource.PartitionKeyPath.Should().Be("/partitionKey");
    }

    [Fact]
    public void BuildPartitionKey_Combines_CustomerId_And_Year_Month()
    {
        var key = HighVolumeOrder.BuildPartitionKey("cust-1", new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero));

        key.Should().Be("cust-1:2026-09");
    }

    [Fact]
    public void Different_Months_Produce_Different_Partition_Keys_For_The_Same_Customer()
    {
        var july = HighVolumeOrder.BuildPartitionKey("cust-1", new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero));
        var august = HighVolumeOrder.BuildPartitionKey("cust-1", new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero));

        july.Should().NotBe(august);
    }

    [Fact]
    public async Task Synthetic_Key_Groups_Same_Month_Orders_Into_One_Partition_Query()
    {
        var customerId = $"cust-{Guid.NewGuid():N}";
        var septemberDate = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var septemberKey = HighVolumeOrder.BuildPartitionKey(customerId, septemberDate);

        for (var i = 0; i < 3; i++)
        {
            var order = new HighVolumeOrder
            {
                CustomerId = customerId,
                OrderDate = septemberDate.AddDays(i),
                PartitionKey = septemberKey,
                OrderLines = [new OrderLine { ProductId = $"sku-{i}", Quantity = 1 }],
            };
            await fixture.HighVolumeOrdersContainer.UpsertItemAsync(order, new PartitionKey(septemberKey));
        }

        var query = fixture.HighVolumeOrdersContainer
            .GetItemLinqQueryable<HighVolumeOrder>(requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(septemberKey) })
            .Where(o => o.PartitionKey == septemberKey);

        using var iterator = query.ToFeedIterator();
        var page = await iterator.ReadNextAsync();

        page.Should().HaveCount(3);
        page.Should().OnlyContain(o => o.PartitionKey == septemberKey);
    }

    [Fact]
    public async Task Query_Scoped_To_One_Month_Excludes_Other_Months_For_Same_Customer()
    {
        var customerId = $"cust-{Guid.NewGuid():N}";
        var julyKey = HighVolumeOrder.BuildPartitionKey(customerId, new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        var augustKey = HighVolumeOrder.BuildPartitionKey(customerId, new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        await fixture.HighVolumeOrdersContainer.UpsertItemAsync(
            new HighVolumeOrder { CustomerId = customerId, PartitionKey = julyKey, OrderLines = [new OrderLine { ProductId = "sku-jul", Quantity = 1 }] },
            new PartitionKey(julyKey));
        await fixture.HighVolumeOrdersContainer.UpsertItemAsync(
            new HighVolumeOrder { CustomerId = customerId, PartitionKey = augustKey, OrderLines = [new OrderLine { ProductId = "sku-aug", Quantity = 1 }] },
            new PartitionKey(augustKey));

        var query = fixture.HighVolumeOrdersContainer
            .GetItemLinqQueryable<HighVolumeOrder>(requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(julyKey) })
            .Where(o => o.PartitionKey == julyKey);

        using var iterator = query.ToFeedIterator();
        var page = await iterator.ReadNextAsync();

        page.Should().ContainSingle();
        page.Single().OrderLines.Should().ContainSingle(l => l.ProductId == "sku-jul");
    }
}
