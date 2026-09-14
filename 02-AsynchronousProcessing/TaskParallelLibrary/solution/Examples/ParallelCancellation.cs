namespace TaskParallelLibrary.Examples;

/// <summary>Part 5: cancelling parallel work.</summary>
public class ParallelCancellation
{
    private static void ProcessItem(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Thread.Sleep(200);
    }

    public void DemonstrateCancellation()
    {
        Console.WriteLine("=== PARALLEL CANCELLATION ===\n");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        var options = new ParallelOptions { CancellationToken = cts.Token };
        var processed = 0;

        try
        {
            Parallel.For(0, 50, options, _ =>
            {
                ProcessItem(cts.Token);
                Interlocked.Increment(ref processed);
            });

            Console.WriteLine("All items processed");
        }
        catch (OperationCanceledException)
        {
            // Setting ParallelOptions.CancellationToken makes the loop throw
            // OperationCanceledException rather than wrapping it in an
            // AggregateException -- which is what you want here.
            Console.WriteLine($"Operation cancelled after {processed} items");
        }
    }

    public void PlinqCancellation()
    {
        Console.WriteLine("\n=== PLINQ CANCELLATION ===\n");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        try
        {
            var results = Enumerable.Range(0, 50)
                .AsParallel()
                .WithCancellation(cts.Token)
                .Select(i => { Thread.Sleep(200); return i; })
                .ToList();

            Console.WriteLine($"Processed {results.Count} items");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("PLINQ cancelled -- partial results are discarded entirely.");
            Console.WriteLine("If you need what finished, collect into a concurrent");
            Console.WriteLine("collection as you go rather than relying on ToList().");
        }
    }
}
