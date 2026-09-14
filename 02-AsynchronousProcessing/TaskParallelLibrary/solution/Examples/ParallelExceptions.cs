namespace TaskParallelLibrary.Examples;

/// <summary>Part 4: parallel failures arrive as an AggregateException.</summary>
public class ParallelExceptionHandling
{
    private static void ProcessItem(int item)
    {
        if (item == 5 || item == 15)
        {
            throw new InvalidOperationException($"Failed to process item {item}");
        }

        Thread.Sleep(50);
    }

    public void HandleParallelExceptions()
    {
        Console.WriteLine("=== PARALLEL EXCEPTION HANDLING ===\n");

        try
        {
            Parallel.For(0, 20, ProcessItem);
        }
        catch (AggregateException ae)
        {
            // Unlike await, Parallel.For gives you every failure at once.
            Console.WriteLine($"Caught {ae.InnerExceptions.Count} exception(s):\n");

            foreach (var ex in ae.InnerExceptions)
            {
                Console.WriteLine($"  - {ex.Message}");
            }
        }

        Console.WriteLine("\nNote: a failure does not stop the loop instantly. Iterations");
        Console.WriteLine("already running finish, and the loop stops scheduling new ones.");
    }

    public void HandlePlinqExceptions()
    {
        Console.WriteLine("\n=== PLINQ EXCEPTION HANDLING ===\n");

        try
        {
            _ = Enumerable.Range(0, 20)
                .AsParallel()
                .Select(i => { ProcessItem(i); return i; })
                .ToList();
        }
        catch (AggregateException ae)
        {
            Console.WriteLine($"PLINQ threw {ae.InnerExceptions.Count} exception(s)");

            // Handle returns false for anything you did not deal with, and
            // those are rethrown as a new AggregateException.
            ae.Handle(ex =>
            {
                if (ex is InvalidOperationException)
                {
                    Console.WriteLine($"  Handled: {ex.Message}");
                    return true;
                }
                return false;
            });
        }
    }

    /// <summary>
    /// Usually you do not want one bad item to stop the batch. Catching inside
    /// the loop body keeps the failure with the item it belongs to.
    /// </summary>
    public void CollectFailuresInstead()
    {
        Console.WriteLine("\n=== COLLECTING FAILURES PER ITEM ===\n");

        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        var succeeded = 0;

        Parallel.For(0, 20, i =>
        {
            try
            {
                ProcessItem(i);
                Interlocked.Increment(ref succeeded);
            }
            catch (InvalidOperationException ex)
            {
                failures.Add(ex.Message);
            }
        });

        Console.WriteLine($"Succeeded: {succeeded}, failed: {failures.Count}");
        Console.WriteLine("Every item was attempted -- nothing was abandoned.");
    }
}
