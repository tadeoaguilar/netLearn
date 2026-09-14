using System.Collections.Concurrent;
using System.Diagnostics;

namespace TaskParallelLibrary.Examples;

/// <summary>Part 1: Parallel.For against a plain for loop.</summary>
public class BasicParallel
{
    /// <summary>
    /// Deliberately wasteful work, so the loop has something to parallelize.
    /// Factorial of 20 on its own is far too fast to measure.
    /// </summary>
    private static long ComputeFactorial(int n)
    {
        long result = 1;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
        }
        return result;
    }

    private static long HeavyWork(int iterations)
    {
        long total = 0;
        for (int i = 0; i < iterations; i++)
        {
            total += ComputeFactorial(20);
        }
        return total;
    }

    public TimeSpan SequentialProcessing()
    {
        Console.WriteLine("=== SEQUENTIAL PROCESSING ===\n");

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
        {
            HeavyWork(200_000);
        }

        sw.Stop();
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Threads used: 1");
        return sw.Elapsed;
    }

    public TimeSpan ParallelProcessing()
    {
        Console.WriteLine("\n=== PARALLEL PROCESSING ===\n");

        var sw = Stopwatch.StartNew();
        var threadIds = new ConcurrentBag<int>();

        Parallel.For(0, 100, i =>
        {
            threadIds.Add(Environment.CurrentManagedThreadId);
            HeavyWork(200_000);
        });

        sw.Stop();
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Threads used: {threadIds.Distinct().Count()}");
        return sw.Elapsed;
    }

    /// <summary>
    /// EXERCISE.md computes speedup as elapsed/elapsed, which is always 1.00x.
    /// A speedup figure needs both measurements.
    /// </summary>
    public void CompareBoth()
    {
        var sequential = SequentialProcessing();
        var parallel = ParallelProcessing();

        Console.WriteLine($"\nSpeedup: {sequential.TotalMilliseconds / parallel.TotalMilliseconds:F2}x " +
                          $"on {Environment.ProcessorCount} cores");
        Console.WriteLine("\nNote: the exercise writes Console.Write inside the parallel loop.");
        Console.WriteLine("Console output takes a process-wide lock, so doing that turns a");
        Console.WriteLine("parallel loop back into a serial one and hides the speedup entirely.");
    }
}
