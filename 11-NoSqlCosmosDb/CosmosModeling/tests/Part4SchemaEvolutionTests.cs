using System.Text;
using CosmosModeling.Domain;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

namespace CosmosModeling.Tests;

[Collection(CosmosModelingCollection.Name)]
public class Part4SchemaEvolutionTests(CosmosModelingFixture fixture)
{
    private async Task WriteOldShapeOrderAsync(string orderId, string customerId)
    {
        var oldShapeDocument = new JObject
        {
            ["id"] = orderId,
            ["customerId"] = customerId,
            ["OrderDate"] = DateTimeOffset.UtcNow.AddMonths(-6).ToString("O"),
            ["Status"] = "Delivered",
            ["OrderLines"] = new JArray
            {
                new JObject { ["ProductId"] = "sku-1", ["ProductName"] = "Keyboard", ["Quantity"] = 1, ["UnitPrice"] = 79.99m },
            },
            // Deliberately absent: SchemaVersion, ShippingAddress.
        };

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(oldShapeDocument.ToString()));
        using var response = await fixture.OrdersContainer.CreateItemStreamAsync(stream, new PartitionKey(customerId));
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Old_Shape_Document_Missing_ShippingAddress_Deserializes_To_Null()
    {
        var customerId = $"cust-{Guid.NewGuid():N}";
        var orderId = Guid.NewGuid().ToString();
        await WriteOldShapeOrderAsync(orderId, customerId);

        var read = await fixture.OrdersContainer.ReadItemAsync<Order>(orderId, new PartitionKey(customerId));

        read.Resource.ShippingAddress.Should().BeNull();
    }

    [Fact]
    public async Task Old_Shape_Document_Missing_SchemaVersion_Defaults_To_Zero()
    {
        var customerId = $"cust-{Guid.NewGuid():N}";
        var orderId = Guid.NewGuid().ToString();
        await WriteOldShapeOrderAsync(orderId, customerId);

        var read = await fixture.OrdersContainer.ReadItemAsync<Order>(orderId, new PartitionKey(customerId));

        // 0 -- the C# int default -- signals "older than any explicit SchemaVersion".
        read.Resource.SchemaVersion.Should().Be(0);
    }

    [Fact]
    public async Task Old_Shape_Document_Still_Has_Its_Embedded_OrderLines()
    {
        var customerId = $"cust-{Guid.NewGuid():N}";
        var orderId = Guid.NewGuid().ToString();
        await WriteOldShapeOrderAsync(orderId, customerId);

        var read = await fixture.OrdersContainer.ReadItemAsync<Order>(orderId, new PartitionKey(customerId));

        read.Resource.OrderLines.Should().ContainSingle(l => l.ProductId == "sku-1");
    }

    [Fact]
    public async Task New_Order_Written_With_ShippingAddress_RoundTrips()
    {
        var customerId = $"cust-{Guid.NewGuid():N}";
        var order = new Order
        {
            CustomerId = customerId,
            OrderLines = [new OrderLine { ProductId = "sku-2", Quantity = 1 }],
            ShippingAddress = new ShippingAddress { Line1 = "1 Infinite Loop", City = "Cupertino", PostalCode = "95014", Country = "US" },
        };

        await fixture.OrdersContainer.UpsertItemAsync(order, new PartitionKey(customerId));
        var read = await fixture.OrdersContainer.ReadItemAsync<Order>(order.Id, new PartitionKey(customerId));

        read.Resource.SchemaVersion.Should().Be(2);
        read.Resource.ShippingAddress.Should().NotBeNull();
        read.Resource.ShippingAddress!.City.Should().Be("Cupertino");
    }

    [Fact]
    public async Task Old_And_New_Shape_Orders_Coexist_In_The_Same_Container()
    {
        var customerId = $"cust-{Guid.NewGuid():N}";
        var oldOrderId = Guid.NewGuid().ToString();
        await WriteOldShapeOrderAsync(oldOrderId, customerId);

        var newOrder = new Order { CustomerId = customerId, OrderLines = [new OrderLine { ProductId = "sku-3", Quantity = 1 }] };
        await fixture.OrdersContainer.UpsertItemAsync(newOrder, new PartitionKey(customerId));

        var oldRead = await fixture.OrdersContainer.ReadItemAsync<Order>(oldOrderId, new PartitionKey(customerId));
        var newRead = await fixture.OrdersContainer.ReadItemAsync<Order>(newOrder.Id, new PartitionKey(customerId));

        oldRead.Resource.ShippingAddress.Should().BeNull();
        newRead.Resource.SchemaVersion.Should().Be(2);
    }
}
