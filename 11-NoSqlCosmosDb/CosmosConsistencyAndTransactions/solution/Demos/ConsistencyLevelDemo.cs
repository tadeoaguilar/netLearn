using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Demos;

/// <summary>
/// Part 1: the five consistency levels, set at the CosmosClient.
/// </summary>
public static class ConsistencyLevelDemo
{
    private static readonly (ConsistencyLevel Level, string Description)[] LevelsStrongestFirst =
    [
        (ConsistencyLevel.Strong,
            "Every read sees the latest committed write, full stop. Highest latency and cost, lowest availability during a regional failure."),
        (ConsistencyLevel.BoundedStaleness,
            "Reads lag writes by at most K versions or T time (both configurable). A tunable, bounded amount of staleness."),
        (ConsistencyLevel.Session,
            "The DEFAULT. Within one session (tracked via a session token -- see Part 2), you always read your own writes. Cheap, and right for most apps."),
        (ConsistencyLevel.ConsistentPrefix,
            "Reads never see writes out of order (no gaps, no reordering), but may be arbitrarily far behind the latest write."),
        (ConsistencyLevel.Eventual,
            "Reads may see writes out of order and arbitrarily stale. Cheapest, most available, weakest guarantee.")
    ];

    public static async Task RunAsync(CosmosClient client, string connectionString, Container customers)
    {
        var account = await client.ReadAccountAsync();
        var accountDefault = account.Consistency.DefaultConsistencyLevel;

        Console.WriteLine($"Account-level default consistency: {accountDefault}");
        Console.WriteLine($"This CosmosClient was constructed with: {client.ClientOptions.ConsistencyLevel?.ToString() ?? "(account default)"}");
        Console.WriteLine();
        Console.WriteLine("The five levels, strongest to weakest:");
        foreach (var (level, description) in LevelsStrongestFirst)
        {
            Console.WriteLine($"  {level,-17} {description}");
        }

        Console.WriteLine();
        Console.WriteLine("IMPORTANT LIMITATION: the local emulator is a single node with no cross-");
        Console.WriteLine("region replicas, so there is no staleness for any level to visibly exhibit --");
        Console.WriteLine("every level below will read back the value you just wrote. What IS real and");
        Console.WriteLine("checkable here: the SDK accepting each level, and the rule that a client (or a");
        Console.WriteLine("request -- see Part 2) can only ask for a level EQUAL TO or WEAKER than the");
        Console.WriteLine("account's configured default. Asking for something STRONGER is rejected.");
        Console.WriteLine();

        foreach (var (level, _) in LevelsStrongestFirst)
        {
            if (Rank(level) > Rank(accountDefault))
            {
                Console.WriteLine($"  {level,-17} SKIPPED -- stronger than the account default ({accountDefault}); not allowed.");
                continue;
            }

            using var probeClient = new CosmosClient(connectionString, new CosmosClientOptions { ConsistencyLevel = level });
            var container = probeClient.GetContainer(customers.Database.Id, customers.Id);

            var probe = new Customer { Name = $"ConsistencyProbe-{level}", Email = "probe@example.com" };
            var response = await container.CreateItemAsync(probe, new PartitionKey(probe.Id));

            Console.WriteLine($"  {level,-17} client constructed and wrote OK (RU charge {response.RequestCharge:0.00}).");
        }
    }

    private static int Rank(ConsistencyLevel level) => level switch
    {
        ConsistencyLevel.Strong => 4,
        ConsistencyLevel.BoundedStaleness => 3,
        ConsistencyLevel.Session => 2,
        ConsistencyLevel.ConsistentPrefix => 1,
        ConsistencyLevel.Eventual => 0,
        _ => -1
    };
}
