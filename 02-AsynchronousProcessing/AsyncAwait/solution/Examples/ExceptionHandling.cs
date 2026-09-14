namespace AsyncAwait.Examples;

/// <summary>Part 4: how exceptions surface from tasks.</summary>
public class ExceptionHandling
{
    private async Task<string> FailingOperationAsync(int id)
    {
        await Task.Delay(1000);

        if (id == 2)
        {
            throw new InvalidOperationException($"Operation {id} failed!");
        }

        return $"Result {id}";
    }

    public async Task DemonstrateSingleException()
    {
        Console.WriteLine("=== SINGLE EXCEPTION ===\n");

        try
        {
            var result = await FailingOperationAsync(2);
            Console.WriteLine(result);
        }
        catch (InvalidOperationException ex)
        {
            // await rethrows the original exception, not an AggregateException.
            Console.WriteLine($"Caught exception: {ex.Message}");
        }
    }

    public async Task DemonstrateMultipleExceptions()
    {
        Console.WriteLine("\n=== MULTIPLE EXCEPTIONS (WhenAll) ===\n");

        var tasks = new[]
        {
            FailingOperationAsync(1),
            FailingOperationAsync(2), // Will throw
            FailingOperationAsync(3)
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            // await surfaces only the FIRST exception. The others are still
            // recorded on their own tasks -- this is the trap.
            Console.WriteLine($"Caught: {ex.Message}\n");

            foreach (var task in tasks.Where(t => t.IsFaulted))
            {
                Console.WriteLine($"Task exception: {task.Exception?.InnerException?.Message}");
            }
        }
    }

    public async Task DemonstrateAllExceptions()
    {
        Console.WriteLine("\n=== SEEING EVERY EXCEPTION ===\n");

        var tasks = new[]
        {
            FailingOperationAsync(2),
            FailingOperationAsync(2)
        };

        var whenAll = Task.WhenAll(tasks);

        try
        {
            await whenAll;
        }
        catch (InvalidOperationException)
        {
            // Reading .Exception on the WhenAll task gives the AggregateException
            // holding every failure, which awaiting alone would have hidden.
            foreach (var inner in whenAll.Exception!.InnerExceptions)
            {
                Console.WriteLine($"Inner: {inner.Message}");
            }
        }
    }
}
