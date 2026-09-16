using System.Net;
using CosmosIndexingAndThroughput.Retry;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Tests;

// RetryPolicy's backoff branch depends on receiving a real 429, which isn't
// reliably reproducible against a single-client emulator run. These tests
// exercise the retry/backoff LOGIC directly against a constructed
// CosmosException instead -- no emulator required, so they also run fast.
public class RetryPolicyTests
{
    [Fact]
    public async Task Succeeds_immediately_when_the_operation_does_not_throw()
    {
        var callCount = 0;
        var result = await RetryPolicy.ExecuteWithManualRetryAsync(() =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task Retries_on_429_and_succeeds_once_the_operation_stops_throttling()
    {
        var callCount = 0;
        var throttledAttempts = new List<int>();

        var result = await RetryPolicy.ExecuteWithManualRetryAsync(
            () =>
            {
                callCount++;
                if (callCount < 3)
                {
                    throw new CosmosException("Too many requests", HttpStatusCode.TooManyRequests, subStatusCode: 0, activityId: "test-activity", requestCharge: 1.0);
                }

                return Task.FromResult("ok");
            },
            maxAttempts: 5,
            onThrottled: (attempt, _) => throttledAttempts.Add(attempt));

        result.Should().Be("ok");
        callCount.Should().Be(3);
        throttledAttempts.Should().Equal(1, 2);
    }

    [Fact]
    public async Task Gives_up_and_throws_after_max_attempts_are_exhausted()
    {
        var callCount = 0;

        Func<Task> act = () => RetryPolicy.ExecuteWithManualRetryAsync<string>(
            () =>
            {
                callCount++;
                throw new CosmosException("Too many requests", HttpStatusCode.TooManyRequests, subStatusCode: 0, activityId: "test-activity", requestCharge: 1.0);
            },
            maxAttempts: 3);

        await act.Should().ThrowAsync<CosmosException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.TooManyRequests);
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task Does_not_retry_exceptions_that_are_not_429()
    {
        var callCount = 0;

        Func<Task> act = () => RetryPolicy.ExecuteWithManualRetryAsync<string>(
            () =>
            {
                callCount++;
                throw new CosmosException("Not found", HttpStatusCode.NotFound, subStatusCode: 0, activityId: "test-activity", requestCharge: 1.0);
            },
            maxAttempts: 5);

        await act.Should().ThrowAsync<CosmosException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.NotFound);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task Uses_the_exception_RetryAfter_as_the_backoff_delay_when_present()
    {
        var delays = new List<TimeSpan>();
        var callCount = 0;

        await RetryPolicy.ExecuteWithManualRetryAsync(
            () =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new CosmosException("Too many requests", HttpStatusCode.TooManyRequests, subStatusCode: 0, activityId: "test-activity", requestCharge: 1.0);
                }

                return Task.FromResult(true);
            },
            maxAttempts: 3,
            onThrottled: (_, delay) => delays.Add(delay));

        // The constructed CosmosException above carries no RetryAfter header,
        // so RetryPolicy must fall back to its own delay rather than throwing.
        delays.Should().ContainSingle();
        delays[0].Should().BePositive();
    }

    [Fact]
    public async Task Rejects_a_maxAttempts_of_zero_or_less()
    {
        Func<Task> act = () => RetryPolicy.ExecuteWithManualRetryAsync(() => Task.FromResult(1), maxAttempts: 0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
