using TaskParallelLibrary.Challenge;
using TaskParallelLibrary.Examples;

namespace TaskParallelLibrary.Demos;

public static class Demos
{
    public static void Part1BasicParallel()
    {
        Console.WriteLine("=== PART 1: PARALLEL.FOR / PARALLEL.FOREACH ===\n");
        new BasicParallel().CompareBoth();
        Console.WriteLine();
        var forEach = new ParallelForEachExample();
        forEach.SequentialForEach();
        forEach.ParallelForEach();
    }

    public static void Part2Plinq()
    {
        Console.WriteLine("=== PART 2: PLINQ ===\n");
        var demo = new PlinqExample();
        demo.SequentialLinq();
        demo.ParallelLinq();
        demo.PlinqWithOrdering();
        demo.WhenPlinqIsSlower();
    }

    public static void Part3Control()
    {
        Console.WriteLine("=== PART 3: CONTROLLING PARALLELISM ===\n");
        var demo = new ParallelismControl();
        demo.DefaultParallelism();
        demo.LimitedParallelism();
        demo.PlinqDegreeOfParallelism();
    }

    public static void Part4Exceptions()
    {
        Console.WriteLine("=== PART 4: EXCEPTIONS ===\n");
        var demo = new ParallelExceptionHandling();
        demo.HandleParallelExceptions();
        demo.HandlePlinqExceptions();
        demo.CollectFailuresInstead();
    }

    public static void Part5Cancellation()
    {
        Console.WriteLine("=== PART 5: CANCELLATION ===\n");
        var demo = new ParallelCancellation();
        demo.DemonstrateCancellation();
        demo.PlinqCancellation();
    }

    public static void Part6Partitioning()
    {
        Console.WriteLine("=== PART 6: PARTITIONING ===\n");
        var demo = new PartitioningExample();
        demo.DefaultPartitioning();
        demo.CustomPartitioning();
    }

    public static async Task Part7AsyncVsParallel()
    {
        Console.WriteLine("=== PART 7: ASYNC VS PARALLEL ===\n");
        await new AsyncVsParallel().DemonstrateTheDifference();
    }

    public static void ChallengeImageProcessor()
    {
        Console.WriteLine("=== CHALLENGE: PARALLEL IMAGE PROCESSOR ===\n");

        var jobs = ParallelImageProcessor.CreateBatch(100);
        var processor = new ParallelImageProcessor(new SimulatedResizeOperation());

        var lastReported = 0;
        var progress = new Progress<int>(done =>
        {
            if (done - lastReported >= 20 || done == jobs.Count)
            {
                lastReported = done;
                Console.WriteLine($"  progress: {done}/{jobs.Count}");
            }
        });

        var stats = processor.ProcessAll(jobs, progress);

        Console.WriteLine($"\n--- Statistics ---");
        Console.WriteLine($"  Images:      {stats.Total}");
        Console.WriteLine($"  Succeeded:   {stats.Succeeded}");
        Console.WriteLine($"  Failed:      {stats.Failed}");
        Console.WriteLine($"  Total time:  {stats.TotalTime.TotalMilliseconds:N0}ms");
        Console.WriteLine($"  Average:     {stats.AverageTime.TotalMilliseconds:N1}ms per image");
        Console.WriteLine($"  Threads:     {stats.ThreadsUsed} (of {Environment.ProcessorCount} cores)");
        Console.WriteLine($"  Success:     {stats.SuccessRate:P0}");

        var slowest = stats.Results.Where(r => r.Succeeded)
            .OrderByDescending(r => r.Duration).Take(3);
        Console.WriteLine($"\n  Slowest three:");
        foreach (var result in slowest)
        {
            Console.WriteLine($"    {result.Name} {result.Duration.TotalMilliseconds,7:N1}ms");
        }

        Console.WriteLine($"\n  Failures:");
        foreach (var failure in stats.Results.Where(r => !r.Succeeded).Take(3))
        {
            Console.WriteLine($"    {failure.Name}: {failure.Error}");
        }

        var serial = stats.Results.Aggregate(TimeSpan.Zero, (sum, r) => sum + r.Duration);
        Console.WriteLine($"\n  Summed per-image time: {serial.TotalMilliseconds:N0}ms");
        Console.WriteLine($"  Actual wall clock:     {stats.TotalTime.TotalMilliseconds:N0}ms");
        Console.WriteLine($"  Effective speedup:     {serial.TotalMilliseconds / stats.TotalTime.TotalMilliseconds:F2}x");
    }
}
