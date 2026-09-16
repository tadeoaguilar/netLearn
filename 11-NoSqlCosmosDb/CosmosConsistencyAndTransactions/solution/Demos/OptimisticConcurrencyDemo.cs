using System.Net;
using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Demos;

/// <summary>
/// Part 3: ETag-based optimistic concurrency -- Cosmos's version of module
/// 10's xmin-based optimistic concurrency.
/// </summary>
public static class OptimisticConcurrencyDemo
{
    public static async Task RunAsync(Container customers)
    {
        var customer = new Customer { Name = "Concurrency Demo", Email = "concurrency@example.com", LoyaltyPoints = 100 };
        await customers.CreateItemAsync(customer, new PartitionKey(customer.Id));
        Console.WriteLine($"Seeded customer {customer.Id} with {customer.LoyaltyPoints} points.");

        // Two "requests" both read the SAME version of the document.
        var readA = await customers.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));
        var readB = await customers.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));
        Console.WriteLine($"Both readers loaded ETag {readA.ETag}, LoyaltyPoints={readA.Resource.LoyaltyPoints}.");

        // Writer A applies its change first, using its ETag as an If-Match
        // precondition.
        var customerA = readA.Resource;
        customerA.LoyaltyPoints += 50;
        var writeA = await customers.ReplaceItemAsync(
            customerA, customerA.Id, new PartitionKey(customerA.Id),
            new ItemRequestOptions { IfMatchEtag = readA.ETag });
        Console.WriteLine($"Writer A succeeded. New ETag {writeA.ETag}, points={writeA.Resource.LoyaltyPoints}.");

        // Writer B still carries the ORIGINAL ETag -- the server-side ETag
        // already moved when A wrote, so B's If-Match precondition fails.
        var customerB = readB.Resource;
        customerB.LoyaltyPoints += 20;
        try
        {
            await customers.ReplaceItemAsync(
                customerB, customerB.Id, new PartitionKey(customerB.Id),
                new ItemRequestOptions { IfMatchEtag = readB.ETag });

            Console.WriteLine("Unexpected: Writer B's stale write succeeded.");
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            Console.WriteLine("Writer B: 412 PreconditionFailed -- someone else changed this document first.");

            // Reload-and-retry: fetch the current document (and its current
            // ETag), reapply B's business intent on top of it, and try again.
            var reloaded = await customers.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));
            reloaded.Resource.LoyaltyPoints += 20;
            var retry = await customers.ReplaceItemAsync(
                reloaded.Resource, reloaded.Resource.Id, new PartitionKey(reloaded.Resource.Id),
                new ItemRequestOptions { IfMatchEtag = reloaded.ETag });
            Console.WriteLine($"Retry succeeded. Final points={retry.Resource.LoyaltyPoints} (100 + 50 + 20).");
        }

        Console.WriteLine();
        Console.WriteLine("Compare to module 10's EfCoreTransactions Part 4: same compare-and-swap idea");
        Console.WriteLine("(read a version marker, write back only if it hasn't moved), but there the");
        Console.WriteLine("marker is Postgres's server-assigned 'xmin' column, checked via a WHERE clause");
        Console.WriteLine("and a rows-affected count that throws DbUpdateConcurrencyException at zero;");
        Console.WriteLine("here it's an ETag, checked via an If-Match HTTP precondition header that fails");
        Console.WriteLine("with a 412 status code. Same problem, different mechanism.");
    }
}
