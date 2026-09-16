using System.Net;
using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Tests;

[Collection("Cosmos emulator")]
public class TransactionalBatchTests(CosmosFixture fixture)
{
    private async Task<(string CustomerId, CustomerLoyaltyProfile Profile, string ETag)> SeedProfileAsync()
    {
        var customerId = Guid.NewGuid().ToString();
        var profile = new CustomerLoyaltyProfile { Id = CustomerLoyaltyProfile.BuildId(customerId), CustomerId = customerId };
        var response = await fixture.Orders.CreateItemAsync(profile, new PartitionKey(customerId));
        return (customerId, response.Resource, response.ETag);
    }

    private static Order NewOrder(string customerId, decimal unitPrice) => new()
    {
        CustomerId = customerId,
        Status = OrderStatus.Confirmed,
        Lines = [new OrderLine { ProductId = "sku-1", ProductName = "Widget", Quantity = 1, UnitPrice = unitPrice }]
    };

    private async Task<bool> TryReadOrderAsync(string id, string customerId)
    {
        try
        {
            await fixture.Orders.ReadItemAsync<Order>(id, new PartitionKey(customerId));
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    [Fact]
    public async Task Batch_creating_an_order_and_updating_the_profile_succeeds()
    {
        var (customerId, profile, etag) = await SeedProfileAsync();
        var order = NewOrder(customerId, 30m);
        profile.LoyaltyPoints += (int)order.TotalAmount;

        var batch = fixture.Orders.CreateTransactionalBatch(new PartitionKey(customerId))
            .CreateItem(order)
            .ReplaceItem(profile.Id, profile, new TransactionalBatchItemRequestOptions { IfMatchEtag = etag });

        using var response = await batch.ExecuteAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Successful_batch_persists_the_new_order()
    {
        var (customerId, profile, etag) = await SeedProfileAsync();
        var order = NewOrder(customerId, 30m);
        profile.LoyaltyPoints += (int)order.TotalAmount;

        var batch = fixture.Orders.CreateTransactionalBatch(new PartitionKey(customerId))
            .CreateItem(order)
            .ReplaceItem(profile.Id, profile, new TransactionalBatchItemRequestOptions { IfMatchEtag = etag });
        using var response = await batch.ExecuteAsync();
        response.IsSuccessStatusCode.Should().BeTrue();

        var readOrder = await fixture.Orders.ReadItemAsync<Order>(order.Id, new PartitionKey(customerId));
        readOrder.Resource.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Successful_batch_persists_the_updated_loyalty_points()
    {
        var (customerId, profile, etag) = await SeedProfileAsync();
        var order = NewOrder(customerId, 40m);
        profile.LoyaltyPoints += (int)order.TotalAmount;

        var batch = fixture.Orders.CreateTransactionalBatch(new PartitionKey(customerId))
            .CreateItem(order)
            .ReplaceItem(profile.Id, profile, new TransactionalBatchItemRequestOptions { IfMatchEtag = etag });
        using var response = await batch.ExecuteAsync();
        response.IsSuccessStatusCode.Should().BeTrue();

        var readProfile = await fixture.Orders.ReadItemAsync<CustomerLoyaltyProfile>(profile.Id, new PartitionKey(customerId));
        readProfile.Resource.LoyaltyPoints.Should().Be(40);
    }

    [Fact]
    public async Task Batch_with_a_stale_ETag_on_one_operation_fails_as_a_whole()
    {
        var (customerId, profile, staleEtag) = await SeedProfileAsync();

        // Move the profile forward so `staleEtag` no longer matches.
        var current = await fixture.Orders.ReadItemAsync<CustomerLoyaltyProfile>(profile.Id, new PartitionKey(customerId));
        current.Resource.LoyaltyPoints = 5;
        await fixture.Orders.ReplaceItemAsync(current.Resource, current.Resource.Id, new PartitionKey(customerId));

        var order = NewOrder(customerId, 999m);
        var conflictingUpdate = new CustomerLoyaltyProfile { Id = profile.Id, CustomerId = customerId, LoyaltyPoints = 999 };

        var batch = fixture.Orders.CreateTransactionalBatch(new PartitionKey(customerId))
            .CreateItem(order)
            .ReplaceItem(profile.Id, conflictingUpdate, new TransactionalBatchItemRequestOptions { IfMatchEtag = staleEtag });

        using var response = await batch.ExecuteAsync();

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Failed_batch_does_not_persist_the_order()
    {
        var (customerId, profile, staleEtag) = await SeedProfileAsync();
        var current = await fixture.Orders.ReadItemAsync<CustomerLoyaltyProfile>(profile.Id, new PartitionKey(customerId));
        current.Resource.LoyaltyPoints = 5;
        await fixture.Orders.ReplaceItemAsync(current.Resource, current.Resource.Id, new PartitionKey(customerId));

        var order = NewOrder(customerId, 999m);
        var batch = fixture.Orders.CreateTransactionalBatch(new PartitionKey(customerId))
            .CreateItem(order)
            .ReplaceItem(profile.Id, new CustomerLoyaltyProfile { Id = profile.Id, CustomerId = customerId, LoyaltyPoints = 999 },
                new TransactionalBatchItemRequestOptions { IfMatchEtag = staleEtag });
        using var response = await batch.ExecuteAsync();
        response.IsSuccessStatusCode.Should().BeFalse();

        var orderExists = await TryReadOrderAsync(order.Id, customerId);
        orderExists.Should().BeFalse();
    }

    [Fact]
    public async Task Failed_batch_does_not_change_the_loyalty_points()
    {
        var (customerId, profile, staleEtag) = await SeedProfileAsync();
        var current = await fixture.Orders.ReadItemAsync<CustomerLoyaltyProfile>(profile.Id, new PartitionKey(customerId));
        current.Resource.LoyaltyPoints = 5;
        await fixture.Orders.ReplaceItemAsync(current.Resource, current.Resource.Id, new PartitionKey(customerId));

        var order = NewOrder(customerId, 999m);
        var batch = fixture.Orders.CreateTransactionalBatch(new PartitionKey(customerId))
            .CreateItem(order)
            .ReplaceItem(profile.Id, new CustomerLoyaltyProfile { Id = profile.Id, CustomerId = customerId, LoyaltyPoints = 999 },
                new TransactionalBatchItemRequestOptions { IfMatchEtag = staleEtag });
        using var response = await batch.ExecuteAsync();
        response.IsSuccessStatusCode.Should().BeFalse();

        var readProfile = await fixture.Orders.ReadItemAsync<CustomerLoyaltyProfile>(profile.Id, new PartitionKey(customerId));
        readProfile.Resource.LoyaltyPoints.Should().Be(5);
    }

    [Fact]
    public async Task Batch_across_two_different_partition_keys_does_not_succeed()
    {
        var customerA = Guid.NewGuid().ToString();
        var customerB = Guid.NewGuid().ToString();
        var orderForB = NewOrder(customerB, 10m);

        TransactionalBatchResponse? response = null;
        Exception? thrown = null;
        try
        {
            var batch = fixture.Orders.CreateTransactionalBatch(new PartitionKey(customerA))
                .CreateItem(orderForB);
            response = await batch.ExecuteAsync();
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        // Either the SDK/service throws, or it returns a non-success status
        // -- either way, Cosmos never treats this as one atomic operation
        // spanning customerA's and customerB's partitions.
        if (response is not null)
        {
            response.IsSuccessStatusCode.Should().BeFalse();
            response.Dispose();
        }
        else
        {
            thrown.Should().NotBeNull();
        }

        var orderExists = await TryReadOrderAsync(orderForB.Id, customerB);
        orderExists.Should().BeFalse();
    }
}
