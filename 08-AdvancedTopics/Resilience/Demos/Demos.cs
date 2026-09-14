using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using Resilience.Services;

namespace Resilience.Demos;

public static class Demos
{
    public static async Task Part1Retry()
    {
        Console.WriteLine("=== PART 1: RETRY ===\n");

        var service = UnreliableService.FailsFirst(2);

        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 3,

                // Exponential backoff with JITTER. Without jitter, every client
                // that failed at the same moment retries at the same moment --
                // a thundering herd that keeps the service down.
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(50),

                OnRetry = args =>
                {
                    Console.WriteLine($"  retry {args.AttemptNumber + 1} after {args.RetryDelay.TotalMilliseconds:F0}ms");
                    return default;
                }
            })
            .Build();

        var result = await pipeline.ExecuteAsync(async token => await service.CallAsync(token));

        Console.WriteLine($"\n  result: {result} (after {service.Calls} calls)");
        Console.WriteLine("\n  Retry only helps with TRANSIENT faults. Retrying a 400 Bad Request");
        Console.WriteLine("  just sends the same wrong request four times.");
    }

    public static async Task Part2RetryCannotFixEverything()
    {
        Console.WriteLine("=== PART 2: WHEN RETRY MAKES THINGS WORSE ===\n");

        var service = UnreliableService.AlwaysFails();

        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(20)
            })
            .Build();

        try
        {
            await pipeline.ExecuteAsync(async token => await service.CallAsync(token));
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"  gave up: {ex.Message}");
        }

        Console.WriteLine($"\n  the dying service received {service.Calls} calls instead of 1");
        Console.WriteLine("\n  Every client doing this multiplies load on a service that is");
        Console.WriteLine("  already failing. That is what the circuit breaker is for.");
    }

    public static async Task Part3CircuitBreaker()
    {
        Console.WriteLine("=== PART 3: CIRCUIT BREAKER ===\n");

        var service = UnreliableService.AlwaysFails();

        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromMilliseconds(500),
                OnOpened = args =>
                {
                    Console.WriteLine($"  >> circuit OPENED for {args.BreakDuration.TotalMilliseconds:F0}ms");
                    return default;
                },
                OnClosed = _ => { Console.WriteLine("  >> circuit closed"); return default; }
            })
            .Build();

        for (var i = 1; i <= 8; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(async token => await service.CallAsync(token));
            }
            catch (BrokenCircuitException)
            {
                // The call never reached the service. It failed instantly.
                Console.WriteLine($"  call {i}: rejected by open circuit (service not contacted)");
            }
            catch (HttpRequestException)
            {
                Console.WriteLine($"  call {i}: failed at the service");
            }
        }

        Console.WriteLine($"\n  8 attempts, but the service only received {service.Calls}.");
        Console.WriteLine("\n  An open circuit fails FAST. The caller gets an immediate error");
        Console.WriteLine("  instead of a timeout, and the failing service gets room to recover.");
    }

    public static async Task Part4Timeout()
    {
        Console.WriteLine("=== PART 4: TIMEOUT ===\n");

        var service = UnreliableService.Slow(TimeSpan.FromSeconds(5));

        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddTimeout(TimeSpan.FromMilliseconds(300))
            .Build();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await pipeline.ExecuteAsync(async token => await service.CallAsync(token));
        }
        catch (TimeoutRejectedException)
        {
            Console.WriteLine($"  timed out after {sw.ElapsedMilliseconds}ms rather than waiting 5000ms");
        }

        Console.WriteLine("\n  A slow dependency is more dangerous than a broken one: every");
        Console.WriteLine("  caller waits, holding a thread and a connection, until the whole");
        Console.WriteLine("  system is tied up waiting on one service. Always set a timeout.");
    }

    public static async Task Part5Fallback()
    {
        Console.WriteLine("=== PART 5: FALLBACK ===\n");

        var service = UnreliableService.AlwaysFails();

        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddFallback(new Polly.Fallback.FallbackStrategyOptions<string>
            {
                FallbackAction = _ => Outcome.FromResultAsValueTask("cached result (stale)"),
                OnFallback = _ => { Console.WriteLine("  primary failed, serving cached data"); return default; }
            })
            .AddRetry(new Polly.Retry.RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(10)
            })
            .Build();

        var result = await pipeline.ExecuteAsync(async token => await service.CallAsync(token));

        Console.WriteLine($"  caller received: {result}");
        Console.WriteLine("\n  A degraded answer often beats an error page. But be deliberate:");
        Console.WriteLine("  serving stale data silently can be worse than failing loudly,");
        Console.WriteLine("  depending on what the data is.");
    }

    public static async Task Part6TheFullPipeline()
    {
        Console.WriteLine("=== PART 6: COMPOSING STRATEGIES ===\n");

        Console.WriteLine("Order matters. Outermost runs first:\n");
        Console.WriteLine("  Fallback  -> last resort if everything below fails");
        Console.WriteLine("  Retry     -> repeats what is below it");
        Console.WriteLine("  Circuit   -> counts failures across all retries");
        Console.WriteLine("  Timeout   -> applies to EACH individual attempt\n");

        var service = UnreliableService.FailsFirst(2);

        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddFallback(new Polly.Fallback.FallbackStrategyOptions<string>
            {
                FallbackAction = _ => Outcome.FromResultAsValueTask("degraded response")
            })
            .AddRetry(new Polly.Retry.RetryStrategyOptions<string>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(20),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string>
            {
                FailureRatio = 0.9,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(5)
            })
            .AddTimeout(TimeSpan.FromSeconds(1))
            .Build();

        var result = await pipeline.ExecuteAsync(async token => await service.CallAsync(token));

        Console.WriteLine($"  result: {result} after {service.Calls} calls");
        Console.WriteLine("\n  Putting timeout INSIDE retry means each attempt gets its own");
        Console.WriteLine("  budget. Putting it outside would time-box all attempts together --");
        Console.WriteLine("  a different policy, and usually not the one you meant.");
    }
}
