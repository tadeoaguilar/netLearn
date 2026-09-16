using CosmosModeling.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosModeling.Tests;

[Collection(CosmosModelingCollection.Name)]
public class Part3EmbeddingVsReferencingTests(CosmosModelingFixture fixture)
{
    [Fact]
    public async Task Reading_Order_Returns_OrderLines_In_The_Same_Round_Trip()
    {
        var customerId = $"cust-{Guid.NewGuid():N}";
        var order = new Order
        {
            CustomerId = customerId,
            OrderLines = [new OrderLine { ProductId = "sku-9", ProductName = "Monitor", Quantity = 1, UnitPrice = 249.00m }],
        };
        await fixture.OrdersContainer.UpsertItemAsync(order, new PartitionKey(customerId));

        // A single ReadItemAsync -- no second call for the lines, because they're embedded.
        var read = await fixture.OrdersContainer.ReadItemAsync<Order>(order.Id, new PartitionKey(customerId));

        read.Resource.OrderLines.Should().ContainSingle(l => l.ProductId == "sku-9");
    }

    [Fact]
    public async Task Orders_For_Different_Customers_Live_In_Different_Partitions()
    {
        var customerAId = $"cust-{Guid.NewGuid():N}";
        var customerBId = $"cust-{Guid.NewGuid():N}";

        var orderA = new Order { CustomerId = customerAId, OrderLines = [new OrderLine { ProductId = "sku-a", Quantity = 1 }] };
        var orderB = new Order { CustomerId = customerBId, OrderLines = [new OrderLine { ProductId = "sku-b", Quantity = 1 }] };

        await fixture.OrdersContainer.UpsertItemAsync(orderA, new PartitionKey(customerAId));
        await fixture.OrdersContainer.UpsertItemAsync(orderB, new PartitionKey(customerBId));

        // Reading order A's id under order B's partition key finds nothing -- proof that
        // partition key, not just id, determines where Cosmos looks for an item.
        var act = async () => await fixture.OrdersContainer.ReadItemAsync<Order>(orderA.Id, new PartitionKey(customerBId));

        await act.Should().ThrowAsync<CosmosException>()
            .Where(e => e.StatusCode == System.Net.HttpStatusCode.NotFound);
    }
}
