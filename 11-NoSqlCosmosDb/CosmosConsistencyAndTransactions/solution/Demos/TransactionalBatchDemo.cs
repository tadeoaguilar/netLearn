using System.Net;
using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Demos;

/// <summary>
/// Part 4: TransactionalBatch -- atomic multi-item writes within one
/// partition key. Placing an order and crediting loyalty points, together,
/// or not at all.
/// </summary>
public static class TransactionalBatchDemo
{
    public static async Task RunAsync(Container orders)
    {
        var customerId = Guid.NewGuid().ToString();
        var profile = new CustomerLoyaltyProfile { Id = CustomerLoyaltyProfile.BuildId(customerId), CustomerId = customerId };
        var profileCreate = await orders.CreateItemAsync(profile, new PartitionKey(customerId));
        Console.WriteLine($"Seeded loyalty profile for customer {customerId}: 0 points, ETag {profileCreate.ETag}.");

        // --- Success case: order + loyalty points, same partition, one batch ---
        var order = NewOrder(customerId, quantity: 3, unitPrice: 25m);
        var earnedPoints = (int)order.TotalAmount; // 1 point per dollar spent.

        var updatedProfile = profileCreate.Resource;
        updatedProfile.LoyaltyPoints += earnedPoints;
        updatedProfile.LifetimeOrderCount += 1;
        updatedProfile.LifetimeSpend += order.TotalAmount;

        var batch = orders.CreateTransactionalBatch(new PartitionKey(customerId))
            .CreateItem(order)
            .ReplaceItem(updatedProfile.Id, updatedProfile, new TransactionalBatchItemRequestOptions { IfMatchEtag = profileCreate.ETag });

        using var batchResponse = await batch.ExecuteAsync();
        if (batchResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"TransactionalBatch succeeded: order {order.Id} created AND loyalty profile updated atomically.");
            Console.WriteLine($"  Order create sub-result:   {batchResponse.GetOperationResultAtIndex<Order>(0).StatusCode}");
            Console.WriteLine($"  Profile replace sub-result: {batchResponse.GetOperationResultAtIndex<CustomerLoyaltyProfile>(1).StatusCode}");
        }
        else
        {
            Console.WriteLine($"Unexpected batch failure: {batchResponse.StatusCode}");
        }

        // --- Failure case: engineer a stale ETag on the profile operation ---
        var staleProfileEtag = profileCreate.ETag; // deliberately OLD -- the profile has since moved on.
        var secondOrder = NewOrder(customerId, quantity: 1, unitPrice: 999m);

        var currentProfile = await orders.ReadItemAsync<CustomerLoyaltyProfile>(updatedProfile.Id, new PartitionKey(customerId));
        var conflictingProfile = currentProfile.Resource;
        conflictingProfile.LoyaltyPoints += (int)secondOrder.TotalAmount;

        var failingBatch = orders.CreateTransactionalBatch(new PartitionKey(customerId))
            .CreateItem(secondOrder)
            .ReplaceItem(conflictingProfile.Id, conflictingProfile, new TransactionalBatchItemRequestOptions { IfMatchEtag = staleProfileEtag });

        using var failingResponse = await failingBatch.ExecuteAsync();
        Console.WriteLine($"Deliberately-conflicting batch status: {failingResponse.StatusCode} (IsSuccessStatusCode={failingResponse.IsSuccessStatusCode})");

        var orderExists = await TryReadOrderAsync(orders, secondOrder.Id, customerId);
        var profileAfterFailure = await orders.ReadItemAsync<CustomerLoyaltyProfile>(updatedProfile.Id, new PartitionKey(customerId));
        Console.WriteLine($"  Order from the failed batch exists? {orderExists} (must be false -- true atomicity).");
        Console.WriteLine($"  Profile points after the failed batch: {profileAfterFailure.Resource.LoyaltyPoints} (unchanged from the successful batch).");
    }

    private static Order NewOrder(string customerId, int quantity, decimal unitPrice) => new()
    {
        CustomerId = customerId,
        Status = OrderStatus.Confirmed,
        Lines = [new OrderLine { ProductId = "sku-1", ProductName = "Widget", Quantity = quantity, UnitPrice = unitPrice }]
    };

    private static async Task<bool> TryReadOrderAsync(Container container, string id, string customerId)
    {
        try
        {
            await container.ReadItemAsync<Order>(id, new PartitionKey(customerId));
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
