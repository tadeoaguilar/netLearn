using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Demos;

/// <summary>
/// Part 2: overriding consistency per-request, and propagating session
/// tokens across CosmosClient instances for read-your-own-writes under
/// Session consistency.
/// </summary>
public static class SessionTokenDemo
{
    public static async Task RunAsync(string connectionString, Container customers)
    {
        var customer = new Customer { Name = "Session Demo", Email = "session@example.com", LoyaltyPoints = 10 };
        var writeResponse = await customers.CreateItemAsync(customer, new PartitionKey(customer.Id));
        Console.WriteLine($"Created customer {customer.Id}. Write response session token: {writeResponse.Headers.Session}");

        // Per-request override: ItemRequestOptions.ConsistencyLevel lets THIS
        // read ask for a level weaker than the client's configured default --
        // never stronger (same rule as Part 1, just scoped to one request).
        var weakReadOptions = new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Eventual };
        var weakRead = await customers.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id), weakReadOptions);
        Console.WriteLine($"Read back with a per-request ConsistencyLevel.Eventual override: LoyaltyPoints={weakRead.Resource.LoyaltyPoints}");

        // Simulate a SECOND process -- e.g. another pod behind a load
        // balancer -- with its own CosmosClient that never saw the write
        // above.
        using var otherClient = new CosmosClient(connectionString, new CosmosClientOptions { ConsistencyLevel = ConsistencyLevel.Session });
        var otherContainer = otherClient.GetContainer(customers.Database.Id, customers.Id);

        // WITHOUT the session token: this client has no proof it should be
        // caught up to a session it was never part of. On this single-node
        // emulator it still reads the value we just wrote -- but on a real,
        // multi-region account under Session consistency that is NOT
        // guaranteed unless the token is flowed, as done below.
        var withoutToken = await otherContainer.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));
        Console.WriteLine($"Second client, no session token propagated: LoyaltyPoints={withoutToken.Resource.LoyaltyPoints}");

        // WITH the session token captured from the write's response headers:
        // this read is guaranteed to be at least as fresh as that write. THIS
        // is what "read your own writes" means once an app has more than one
        // CosmosClient instance.
        var withTokenOptions = new ItemRequestOptions { SessionToken = writeResponse.Headers.Session };
        var withToken = await otherContainer.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id), withTokenOptions);
        Console.WriteLine($"Second client, WITH propagated session token: LoyaltyPoints={withToken.Resource.LoyaltyPoints}");
    }
}
