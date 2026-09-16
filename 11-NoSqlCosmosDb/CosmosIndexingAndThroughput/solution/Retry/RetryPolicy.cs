using System.Net;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Retry;

// Manual retry-with-backoff for 429 (TooManyRequests). The Cosmos SDK
// already retries 429s on its own -- see CosmosClientOptions
// .MaxRetryAttemptsOnRateLimitedRequests / .MaxRetryWaitTimeOnRateLimitedRequests
// -- so this is NOT needed just to survive throttling. It exists for the
// cases where you want your own visibility into sustained throttling (log
// it, raise an alert, fall back to a cache) on top of what the SDK already
// retries silently.
public static class RetryPolicy
{
    public static async Task<T> ExecuteWithManualRetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        Action<int, TimeSpan>? onThrottled = null)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "At least one attempt is required.");
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (CosmosException ex) when (ex.StatusCode == (HttpStatusCode)429 && attempt < maxAttempts)
            {
                var delay = ex.RetryAfter ?? TimeSpan.FromMilliseconds(500 * attempt);
                onThrottled?.Invoke(attempt, delay);
                await Task.Delay(delay);
            }
        }
    }
}
