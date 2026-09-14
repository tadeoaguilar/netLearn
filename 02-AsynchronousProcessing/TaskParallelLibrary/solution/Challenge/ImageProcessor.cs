using System.Collections.Concurrent;
using System.Diagnostics;

namespace TaskParallelLibrary.Challenge;

public record ImageJob(string Name, int Width, int Height)
{
    public int Pixels => Width * Height;
}

public record ImageResult(
    string Name,
    bool Succeeded,
    TimeSpan Duration,
    int ThreadId,
    string? Error);

public record ProcessingStatistics(
    int Total,
    int Succeeded,
    int Failed,
    TimeSpan TotalTime,
    TimeSpan AverageTime,
    int ThreadsUsed,
    IReadOnlyList<ImageResult> Results)
{
    public double SuccessRate => Total == 0 ? 0 : (double)Succeeded / Total;
}

/// <summary>
/// The work done to one image. An interface rather than hard-coded pixel
/// pushing, so the tests can make an image fail, hang, or take a known amount
/// of time on demand.
/// </summary>
public interface IImageOperation
{
    void Apply(ImageJob job, CancellationToken cancellationToken);
}

/// <summary>Simulated CPU-bound work, scaled by image size.</summary>
public class SimulatedResizeOperation : IImageOperation
{
    public void Apply(ImageJob job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (job.Name.Contains("corrupt"))
        {
            throw new InvalidDataException($"{job.Name} is not a valid image");
        }

        // Real arithmetic rather than Thread.Sleep -- this is meant to be a
        // CPU-bound workload, and sleeping would make it fake I/O instead.
        double accumulator = 0;
        var iterations = job.Pixels / 2;

        for (var i = 1; i <= iterations; i++)
        {
            if ((i & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            accumulator += Math.Sqrt(i) * Math.Log(i + 1);
        }

        GC.KeepAlive(accumulator);
    }
}

/// <summary>
/// Processes a batch of images across all available cores, collecting
/// per-image timings and failures without letting one bad image stop the run.
/// </summary>
public class ParallelImageProcessor
{
    private readonly IImageOperation _operation;
    private readonly int? _maxDegreeOfParallelism;

    public ParallelImageProcessor(IImageOperation operation, int? maxDegreeOfParallelism = null)
    {
        _operation = operation;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    public ProcessingStatistics ProcessAll(
        IReadOnlyList<ImageJob> jobs,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // ConcurrentBag because every thread writes to it. A List<T> here
        // would corrupt silently rather than throw.
        var results = new ConcurrentBag<ImageResult>();
        var completed = 0;

        var options = new ParallelOptions { CancellationToken = cancellationToken };
        if (_maxDegreeOfParallelism is { } max)
        {
            options.MaxDegreeOfParallelism = max;
        }

        var overall = Stopwatch.StartNew();

        Parallel.ForEach(jobs, options, job =>
        {
            var sw = Stopwatch.StartNew();
            var threadId = Environment.CurrentManagedThreadId;

            try
            {
                _operation.Apply(job, cancellationToken);
                sw.Stop();
                results.Add(new ImageResult(job.Name, true, sw.Elapsed, threadId, null));
            }
            catch (OperationCanceledException)
            {
                // Cancellation is not an image failure -- let Parallel.ForEach
                // surface it instead of recording it as a bad image.
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new ImageResult(job.Name, false, sw.Elapsed, threadId, ex.Message));
            }

            // Interlocked because ++ is not atomic: read, add and write are
            // three steps, and two threads can interleave between them.
            progress?.Report(Interlocked.Increment(ref completed));
        });

        overall.Stop();

        var all = results.ToArray();
        var successes = all.Where(r => r.Succeeded).ToArray();

        return new ProcessingStatistics(
            Total: all.Length,
            Succeeded: successes.Length,
            Failed: all.Length - successes.Length,
            TotalTime: overall.Elapsed,
            AverageTime: successes.Length == 0
                ? TimeSpan.Zero
                : TimeSpan.FromTicks((long)successes.Average(r => r.Duration.Ticks)),
            ThreadsUsed: all.Select(r => r.ThreadId).Distinct().Count(),
            Results: all);
    }

    /// <summary>Builds a batch with a mix of sizes and a few corrupt files.</summary>
    public static IReadOnlyList<ImageJob> CreateBatch(int count)
    {
        var random = new Random(42); // Fixed seed: the demo is reproducible.

        return Enumerable.Range(1, count)
            .Select(i =>
            {
                var corrupt = i % 17 == 0;
                var side = random.Next(400, 1600);
                return new ImageJob(
                    corrupt ? $"corrupt-{i:D3}.png" : $"photo-{i:D3}.jpg",
                    side,
                    side);
            })
            .ToList();
    }
}
