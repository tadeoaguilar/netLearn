using CosmosModeling.Domain;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmosModeling.Tests;

[Collection(CosmosModelingCollection.Name)]
public class Part2OrderDocumentTests(CosmosModelingFixture fixture)
{
    [Fact]
    public async Task Orders_Container_Has_PartitionKeyPath_CustomerId()
    {
        var response = await fixture.OrdersContainer.ReadContainerAsync();

        response.Resource.PartitionKeyPath.Should().Be("/customerId");
    }

    [Fact]
    public async Task Order_RoundTrips_With_Embedded_OrderLines()
    {
        var customerId = $"cust-{Guid.NewGuid():N}";
        var order = new Order
        {
            CustomerId = customerId,
            Status = "Placed",
            OrderLines =
            [
                new OrderLine { ProductId = "sku-1", ProductName = "Keyboard", Quantity = 1, UnitPrice = 79.99m },
                new OrderLine { ProductId = "sku-2", ProductName = "Mouse", Quantity = 2, UnitPrice = 24.99m },
            ],
        };

        await fixture.OrdersContainer.UpsertItemAsync(order, new PartitionKey(customerId));

        var read = await fixture.OrdersContainer.ReadItemAsync<Order>(order.Id, new PartitionKey(customerId));

        read.Resource.OrderLines.Should().HaveCount(2);
        read.Resource.OrderLines.Should().Contain(l => l.ProductId == "sku-1" && l.Quantity == 1);
        read.Resource.OrderLines.Should().Contain(l => l.ProductId == "sku-2" && l.Quantity == 2);
    }

    [Fact]
    public async Task Single_Partition_Query_Returns_Only_That_Customers_Orders()
    {
        var customerId = $"cust-{Guid.NewGuid():N}";
        var otherCustomerId = $"cust-{Guid.NewGuid():N}";

        await fixture.OrdersContainer.UpsertItemAsync(
            new Order { CustomerId = customerId, OrderLines = [new OrderLine { ProductId = "sku-1", Quantity = 1 }] },
            new PartitionKey(customerId));
        await fixture.OrdersContainer.UpsertItemAsync(
            new Order { CustomerId = customerId, OrderLines = [new OrderLine { ProductId = "sku-2", Quantity = 1 }] },
            new PartitionKey(customerId));
        await fixture.OrdersContainer.UpsertItemAsync(
            new Order { CustomerId = otherCustomerId, OrderLines = [new OrderLine { ProductId = "sku-3", Quantity = 1 }] },
            new PartitionKey(otherCustomerId));

        var query = fixture.OrdersContainer
            .GetItemLinqQueryable<Order>(requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(customerId) })
            .Where(o => o.CustomerId == customerId);

        using var iterator = query.ToFeedIterator();
        var page = await iterator.ReadNextAsync();

        page.Should().HaveCount(2);
        page.Should().OnlyContain(o => o.CustomerId == customerId);
    }
}
