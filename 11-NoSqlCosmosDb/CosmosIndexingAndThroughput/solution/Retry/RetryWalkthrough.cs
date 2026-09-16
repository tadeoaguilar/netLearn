using CosmosIndexingAndThroughput.Data;
using CosmosIndexingAndThroughput.Models;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Retry;

public static class RetryWalkthrough
{
    public static async Task RunAsync(Container orders, IReadOnlyList<Customer> customers, string connectionString)
    {
        Console.WriteLine("=== Part 5: Handling 429 TooManyRequests ===");

        var target = customers[0];
        var result = await RetryPolicy.ExecuteWithManualRetryAsync(
            () => orders.UpsertItemAsync(SampleOrderFactory.Create(target.Id, "retry-demo"), new PartitionKey(target.Id)),
            maxAttempts: 3,
            onThrottled: (attempt, delay) =>
                Console.WriteLine($"Throttled on attempt {attempt}, backing off {delay.TotalMilliseconds:F0} ms before retrying."));

        Console.WriteLine($"Write succeeded (charge {result.RequestCharge:F2} RU).");
        Console.WriteLine("A single lightly-loaded client rarely gets throttled by the emulator, so the backoff branch");
        Console.WriteLine("above likely didn't fire here -- RetryPolicy's retry/backoff logic is exercised directly");
        Console.WriteLine("against a constructed 429 CosmosException in the test project instead of depending on real");
        Console.WriteLine("throttling, which isn't reliably reproducible against the emulator.");
        Console.WriteLine();

        Console.WriteLine("The SDK you're already using retries 429s on its own:");
        var resilientOptions = new CosmosClientOptions
        {
            MaxRetryAttemptsOnRateLimitedRequests = 9,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30),
        };
        using (new CosmosClient(connectionString, resilientOptions))
        {
            Console.WriteLine($"  MaxRetryAttemptsOnRateLimitedRequests = {resilientOptions.MaxRetryAttemptsOnRateLimitedRequests}");
            Console.WriteLine($"  MaxRetryWaitTimeOnRateLimitedRequests = {resilientOptions.MaxRetryWaitTimeOnRateLimitedRequests}");
        }

        Console.WriteLine("Reach for manual retry logic like RetryPolicy above when you need your OWN signal on sustained");
        Console.WriteLine("throttling -- logging, alerting, shedding load -- not just to survive an occasional 429; the");
        Console.WriteLine("SDK default already does that part for you.");
    }
}
