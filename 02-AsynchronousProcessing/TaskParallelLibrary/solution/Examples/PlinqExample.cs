using System.Diagnostics;

namespace TaskParallelLibrary.Examples;

/// <summary>Part 2: PLINQ.</summary>
public class PlinqExample
{
    public static bool IsPrime(int number)
    {
        if (number < 2) return false;
        if (number == 2) return true;
        if (number % 2 == 0) return false;

        int limit = (int)Math.Sqrt(number);
        for (int i = 3; i <= limit; i += 2)
        {
            if (number % i == 0) return false;
        }
        return true;
    }

    public void SequentialLinq()
    {
        Console.WriteLine("=== SEQUENTIAL LINQ ===\n");

        var sw = Stopwatch.StartNew();
        var primes = Enumerable.Range(1, 2_000_000).Where(IsPrime).ToList();
        sw.Stop();

        Console.WriteLine($"Found {primes.Count} prime numbers");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
    }

    public void ParallelLinq()
    {
        Console.WriteLine("\n=== PARALLEL LINQ (PLINQ) ===\n");

        var sw = Stopwatch.StartNew();
        var primes = Enumerable.Range(1, 2_000_000).AsParallel().Where(IsPrime).ToList();
        sw.Stop();

        Console.WriteLine($"Found {primes.Count} prime numbers");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine("\nSame count, but the list is NOT in ascending order --");
        Console.WriteLine("partitions finish at different times and results are merged");
        Console.WriteLine("as they arrive. If order matters you must ask for it.");
    }

    public void PlinqWithOrdering()
    {
        Console.WriteLine("\n=== PLINQ WITH ORDERING ===\n");

        var sw = Stopwatch.StartNew();
        var primes = Enumerable.Range(1, 2_000_000)
            .AsParallel()
            .AsOrdered()    // Preserve source order -- costs time
            .Where(IsPrime)
            .ToList();
        sw.Stop();

        Console.WriteLine($"Found {primes.Count} prime numbers (ordered)");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"First 10: {string.Join(", ", primes.Take(10))}");
    }

    /// <summary>
    /// PLINQ is not automatically faster. On cheap per-item work the
    /// partitioning and merging overhead costs more than it saves.
    /// </summary>
    public void WhenPlinqIsSlower()
    {
        Console.WriteLine("\n=== WHEN PLINQ LOSES ===\n");

        var source = Enumerable.Range(1, 5_000_000).ToArray();

        var sw = Stopwatch.StartNew();
        var sequential = source.Select(i => i * 2).Sum(i => (long)i);
        var sequentialTime = sw.ElapsedMilliseconds;

        sw.Restart();
        var parallel = source.AsParallel().Select(i => i * 2).Sum(i => (long)i);
        var parallelTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"Sequential: {sequentialTime}ms");
        Console.WriteLine($"PLINQ:      {parallelTime}ms");
        Console.WriteLine($"Same answer: {sequential == parallel}");
        Console.WriteLine("\nMultiplying by two is too cheap to be worth a partition.");
        Console.WriteLine("Measure before reaching for AsParallel().");
    }
}
