using System.Collections.Concurrent;

namespace TaskParallelLibrary.Examples;

/// <summary>Part 6: how work is divided between threads.</summary>
public class PartitioningExample
{
    public void DefaultPartitioning()
    {
        Console.WriteLine("=== RANGE PARTITIONING ===\n");

        var ranges = new ConcurrentBag<string>();

        Parallel.ForEach(Partitioner.Create(0, 100), range =>
        {
            ranges.Add($"[{range.Item1}, {range.Item2})");

            for (int i = range.Item1; i < range.Item2; i++)
            {
                Thread.Sleep(2);
            }
        });

        Console.WriteLine($"Split into {ranges.Count} ranges:");
        foreach (var range in ranges.OrderBy(r => r))
        {
            Console.WriteLine($"  {range}");
        }
    }

    public void CustomPartitioning()
    {
        Console.WriteLine("\n=== FIXED CHUNK SIZE (10) ===\n");

        var chunks = new ConcurrentBag<string>();

        Parallel.ForEach(Partitioner.Create(0, 100, 10), range =>
        {
            chunks.Add($"[{range.Item1}, {range.Item2})");

            for (int i = range.Item1; i < range.Item2; i++)
            {
                Thread.Sleep(2);
            }
        });

        Console.WriteLine($"Split into {chunks.Count} chunks of 10");
        Console.WriteLine("\nSmaller chunks balance uneven workloads better but cost more");
        Console.WriteLine("coordination. Larger chunks are cheaper but one slow chunk");
        Console.WriteLine("leaves other threads idle at the end.");
    }
}
