using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Demos;

/// <summary>
/// Part 5: what happens when you need atomicity across TWO different
/// partition keys -- and what to do about it.
/// </summary>
public static class CrossPartitionLimitationsDemo
{
    public static async Task RunAsync(Container orders)
    {
        var customerA = Guid.NewGuid().ToString();
        var customerB = Guid.NewGuid().ToString();

        Console.WriteLine("Attempting a TransactionalBatch across TWO different customers' partitions...");
        var orderForB = new Order
        {
            CustomerId = customerB,
            Status = OrderStatus.Confirmed,
            Lines = [new OrderLine { ProductId = "sku-x", ProductName = "Cross-partition widget", Quantity = 1, UnitPrice = 10m }]
        };

        try
        {
            // CreateTransactionalBatch is scoped to ONE partition key
            // (customerA's, here). There is no server-side mechanism that
            // could make an operation on customerB's document atomic with
            // it -- Cosmos has no concept of a transaction spanning
            // partition keys, full stop.
            var batch = orders.CreateTransactionalBatch(new PartitionKey(customerA))
                .CreateItem(orderForB); // orderForB.CustomerId != customerA

            using var response = await batch.ExecuteAsync();
            Console.WriteLine($"Batch call returned: {response.StatusCode} (IsSuccessStatusCode={response.IsSuccessStatusCode})");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"  As expected: Cosmos rejected the mismatched partition key. {response.ErrorMessage}");
            }
        }
        catch (Exception ex) when (ex is CosmosException or ArgumentException)
        {
            Console.WriteLine($"As expected: rejected before it could do anything -- {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("There is no client SDK flag, request option, or server setting that makes a");
        Console.WriteLine("TransactionalBatch -- or any Cosmos operation -- atomic across partition keys.");
        Console.WriteLine("Real-world workarounds when you genuinely need this:");
        Console.WriteLine("  1. Redesign the partition key so documents that must move together share");
        Console.WriteLine("     one. This is exactly what Part 4's CustomerLoyaltyProfile did: it moved");
        Console.WriteLine("     a copy of the data that needed atomicity into the Order's partition.");
        Console.WriteLine("  2. Sagas / compensating actions: apply each partition's change as its own");
        Console.WriteLine("     step, record progress (e.g. a 'saga' or 'process' document), and if a");
        Console.WriteLine("     later step fails, run explicit compensating writes to undo the earlier");
        Console.WriteLine("     ones. You own the rollback logic; Cosmos does not provide it for you.");
        Console.WriteLine("  3. Accept eventual consistency between the two partitions and reconcile");
        Console.WriteLine("     asynchronously -- e.g. a change feed processor (see CosmosChangeFeed).");
    }
}
