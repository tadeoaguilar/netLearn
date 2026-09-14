using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Resilience.Services;

namespace AdvancedTopics.Tests;

public class ResilienceTests
{
    [Fact]
    public async Task Retry_recovers_from_a_transient_fault()
    {
        var service = UnreliableService.FailsFirst(2);
        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero
            })
            .Build();

        var result = await pipeline.ExecuteAsync(async token => await service.CallAsync(token));

        result.Should().Be("ok (call 3)");
        service.Calls.Should().Be(3);
    }

    [Fact]
    public async Task Retry_gives_up_and_amplifies_load_on_a_persistent_fault()
    {
        // Retry is the wrong tool here, and the test says so: four calls to a
        // service that was already failing.
        var service = UnreliableService.AlwaysFails();
        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero
            })
            .Build();

        var act = async () => await pipeline.ExecuteAsync(async token => await service.CallAsync(token));

        await act.Should().ThrowAsync<HttpRequestException>();
        service.Calls.Should().Be(4, "the original attempt plus three retries");
    }

    [Fact]
    public async Task An_open_circuit_stops_calling_the_failing_service()
    {
        var service = UnreliableService.AlwaysFails();
        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .Build();

        var rejected = 0;

        for (var i = 0; i < 10; i++)
        {
            try { await pipeline.ExecuteAsync(async token => await service.CallAsync(token)); }
            catch (BrokenCircuitException) { rejected++; }
            catch (HttpRequestException) { /* reached the service */ }
        }

        rejected.Should().BeGreaterThan(0, "the circuit opened");
        service.Calls.Should().BeLessThan(10, "later calls never reached it");
    }

    [Fact]
    public async Task An_open_circuit_fails_fast()
    {
        var service = UnreliableService.AlwaysFails();
        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 2,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .Build();

        for (var i = 0; i < 4; i++)
        {
            try { await pipeline.ExecuteAsync(async token => await service.CallAsync(token)); }
            catch { /* opening the circuit */ }
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { await pipeline.ExecuteAsync(async token => await service.CallAsync(token)); }
        catch (BrokenCircuitException) { /* expected */ }

        sw.ElapsedMilliseconds.Should().BeLessThan(50, "a rejected call does no work at all");
    }

    [Fact]
    public async Task A_timeout_bounds_a_slow_dependency()
    {
        var service = UnreliableService.Slow(TimeSpan.FromSeconds(10));
        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddTimeout(TimeSpan.FromMilliseconds(100))
            .Build();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var act = async () => await pipeline.ExecuteAsync(async token => await service.CallAsync(token));

        await act.Should().ThrowAsync<TimeoutRejectedException>();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task A_fallback_turns_a_failure_into_a_degraded_answer()
    {
        var service = UnreliableService.AlwaysFails();
        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddFallback(new Polly.Fallback.FallbackStrategyOptions<string>
            {
                FallbackAction = _ => Outcome.FromResultAsValueTask("cached")
            })
            .Build();

        var result = await pipeline.ExecuteAsync(async token => await service.CallAsync(token));

        result.Should().Be("cached");
    }

    [Fact]
    public async Task A_timeout_inside_retry_applies_per_attempt()
    {
        // Ordering check: each attempt gets its own budget. Outside retry, the
        // timeout would box all attempts together -- a different policy.
        var service = UnreliableService.Slow(TimeSpan.FromSeconds(5));
        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder<string>().Handle<TimeoutRejectedException>()
            })
            .AddTimeout(TimeSpan.FromMilliseconds(80))
            .Build();

        var act = async () => await pipeline.ExecuteAsync(async token => await service.CallAsync(token));

        await act.Should().ThrowAsync<TimeoutRejectedException>();
        service.Calls.Should().Be(3, "each attempt timed out on its own budget");
    }

    [Fact]
    public async Task A_full_pipeline_recovers_without_reaching_the_fallback()
    {
        var service = UnreliableService.FailsFirst(2);
        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddFallback(new Polly.Fallback.FallbackStrategyOptions<string>
            {
                FallbackAction = _ => Outcome.FromResultAsValueTask("degraded")
            })
            .AddRetry(new Polly.Retry.RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero
            })
            .AddTimeout(TimeSpan.FromSeconds(1))
            .Build();

        var result = await pipeline.ExecuteAsync(async token => await service.CallAsync(token));

        result.Should().Be("ok (call 3)", "retry fixed it, so the fallback was never needed");
    }
}
