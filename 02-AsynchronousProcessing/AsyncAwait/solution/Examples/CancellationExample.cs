namespace AsyncAwait.Examples;

/// <summary>Part 3: cooperative cancellation.</summary>
public class CancellationExample
{
    public async Task LongRunningOperationAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[Operation] Starting long operation...");

        for (int i = 1; i <= 10; i++)
        {
            // Two separate checks: this one covers the gap between awaits...
            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine($"[Operation] Processing step {i}/10");

            // ...and passing the token here cancels the wait itself, rather
            // than waiting out the full 500ms before noticing.
            await Task.Delay(500, cancellationToken);
        }

        Console.WriteLine("[Operation] Operation completed successfully!");
    }

    public async Task DemonstrateCancellation()
    {
        Console.WriteLine("=== CANCELLATION EXAMPLE ===\n");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            await LongRunningOperationAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n[Operation] Operation was cancelled!");
        }
    }

    public async Task DemonstrateManualCancellation()
    {
        Console.WriteLine("\n=== MANUAL CANCELLATION ===\n");

        using var cts = new CancellationTokenSource();

        var operation = LongRunningOperationAsync(cts.Token);

        var canceller = Task.Run(async () =>
        {
            await Task.Delay(2000);
            Console.WriteLine("\n[User] Requesting cancellation...\n");
            cts.Cancel();
        });

        try
        {
            await operation;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Operation] Cancelled by user request!");
        }

        // The exercise writes this as `_ = Task.Run(...)`. Awaiting it instead
        // means an exception inside the canceller is not silently swallowed.
        await canceller;
    }
}
