using System.Collections.Concurrent;
using System.Diagnostics;

namespace TaskParallelLibrary.Examples;

/// <summary>Part 3: capping how much runs at once.</summary>
public class ParallelismControl
{
    private static void ExpensiveOperation() => Thread.Sleep(100);

    public void DefaultParallelism()
    {
        Console.WriteLine("=== DEFAULT PARALLELISM ===\n");
        Console.WriteLine($"Processor count: {Environment.ProcessorCount}");

        var threads = new ConcurrentBag<int>();
        var sw = Stopwatch.StartNew();

        Parallel.For(0, 20, i =>
        {
            threads.Add(Environment.CurrentManagedThreadId);
            ExpensiveOperation();
        });

        sw.Stop();
        Console.WriteLine($"Distinct threads: {threads.Distinct().Count()}");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
    }

    public void LimitedParallelism()
    {
        Console.WriteLine("\n=== LIMITED PARALLELISM (Max 2) ===\n");

        var threads = new ConcurrentBag<int>();
        var sw = Stopwatch.StartNew();
        var options = new ParallelOptions { MaxDegreeOfParallelism = 2 };

        Parallel.For(0, 20, options, i =>
        {
            threads.Add(Environment.CurrentManagedThreadId);
            ExpensiveOperation();
        });

        sw.Stop();
        Console.WriteLine($"Distinct threads: {threads.Distinct().Count()}");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine("\nTwo at a time means 10 rounds of 100ms -- about 1000ms.");
        Console.WriteLine("Cap parallelism when the bottleneck is a shared resource:");
        Console.WriteLine("a connection pool, an API rate limit, or available memory.");
    }

    public void PlinqDegreeOfParallelism()
    {
        Console.WriteLine("\n=== PLINQ WITH CUSTOM PARALLELISM ===\n");

        var results = Enumerable.Range(1, 100)
            .AsParallel()
            .WithDegreeOfParallelism(4)
            .Select(i =>
            {
                Thread.Sleep(10);
                return i * i;
            })
            .ToList();

        Console.WriteLine($"Processed {results.Count} items with at most 4 threads");
    }
}
