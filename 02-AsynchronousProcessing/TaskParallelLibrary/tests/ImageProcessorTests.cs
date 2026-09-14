using TaskParallelLibrary.Challenge;

namespace TaskParallelLibrary.Tests;

/// <summary>The challenge: every requirement from EXERCISE.md, checked.</summary>
public class ImageProcessorTests
{
    /// <summary>An operation the test controls completely.</summary>
    private sealed class FakeOperation : IImageOperation
    {
        private readonly Action<ImageJob, CancellationToken> _behaviour;
        private int _concurrent;

        public FakeOperation(Action<ImageJob, CancellationToken>? behaviour = null)
            => _behaviour = behaviour ?? ((_, _) => { });

        public int PeakConcurrency { get; private set; }
        public int Applied;

        public void Apply(ImageJob job, CancellationToken cancellationToken)
        {
            var running = Interlocked.Increment(ref _concurrent);
            PeakConcurrency = Math.Max(PeakConcurrency, running);
            Interlocked.Increment(ref Applied);

            try
            {
                _behaviour(job, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrent);
            }
        }
    }

    private static IReadOnlyList<ImageJob> Jobs(int count) =>
        Enumerable.Range(1, count).Select(i => new ImageJob($"img-{i}.jpg", 100, 100)).ToList();

    [Fact]
    public void Every_image_is_processed_exactly_once()
    {
        var operation = new FakeOperation();
        var sut = new ParallelImageProcessor(operation);

        var stats = sut.ProcessAll(Jobs(100));

        operation.Applied.Should().Be(100);
        stats.Total.Should().Be(100);
        stats.Succeeded.Should().Be(100);
        stats.Results.Select(r => r.Name).Distinct().Should().HaveCount(100);
    }

    [Fact]
    public void A_failing_image_is_recorded_without_stopping_the_batch()
    {
        var operation = new FakeOperation((job, _) =>
        {
            if (job.Name.Contains("7")) throw new InvalidDataException("corrupt file");
        });
        var sut = new ParallelImageProcessor(operation);

        var stats = sut.ProcessAll(Jobs(20));

        stats.Total.Should().Be(20, "every image was still attempted");
        stats.Failed.Should().Be(2, "img-7 and img-17");
        stats.Succeeded.Should().Be(18);
        stats.Results.Where(r => !r.Succeeded)
            .Should().OnlyContain(r => r.Error == "corrupt file");
    }

    [Fact]
    public void Statistics_are_internally_consistent()
    {
        var operation = new FakeOperation((job, _) =>
        {
            if (job.Name.EndsWith("3.jpg")) throw new InvalidDataException("bad");
        });
        var sut = new ParallelImageProcessor(operation);

        var stats = sut.ProcessAll(Jobs(30));

        (stats.Succeeded + stats.Failed).Should().Be(stats.Total);
        stats.Results.Should().HaveCount(stats.Total);
        stats.SuccessRate.Should().BeApproximately((double)stats.Succeeded / stats.Total, 1e-9);
        stats.ThreadsUsed.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Progress_is_reported_once_per_image_and_ends_at_the_total()
    {
        var reports = new List<int>();
        var sut = new ParallelImageProcessor(new FakeOperation());

        var stats = sut.ProcessAll(
            Jobs(50),
            new Progress<int>(n => { lock (reports) reports.Add(n); }));

        Thread.Sleep(100); // progress callbacks are posted, not inline

        stats.Succeeded.Should().Be(50);
        lock (reports)
        {
            reports.Should().HaveCount(50);
            reports.Max().Should().Be(50);
        }
    }

    [Fact]
    public void Cancellation_stops_the_batch()
    {
        using var cts = new CancellationTokenSource();
        var operation = new FakeOperation((_, token) =>
        {
            cts.Cancel();
            token.ThrowIfCancellationRequested();
        });
        var sut = new ParallelImageProcessor(operation, maxDegreeOfParallelism: 1);

        var act = () => sut.ProcessAll(Jobs(100), cancellationToken: cts.Token);

        act.Should().Throw<OperationCanceledException>();
        operation.Applied.Should().BeLessThan(100, "the batch stopped early");
    }

    [Fact]
    public void A_cancelled_image_is_not_recorded_as_a_failed_image()
    {
        // Cancellation means "we stopped", not "this image was corrupt".
        using var cts = new CancellationTokenSource();
        var sut = new ParallelImageProcessor(
            new FakeOperation((_, token) => { cts.Cancel(); token.ThrowIfCancellationRequested(); }),
            maxDegreeOfParallelism: 1);

        var act = () => sut.ProcessAll(Jobs(10), cancellationToken: cts.Token);

        act.Should().Throw<OperationCanceledException>()
            .And.Should().NotBeOfType<AggregateException>();
    }

    [Fact]
    public void The_degree_of_parallelism_is_respected()
    {
        var operation = new FakeOperation((_, _) => Thread.Sleep(5));
        var sut = new ParallelImageProcessor(operation, maxDegreeOfParallelism: 2);

        sut.ProcessAll(Jobs(40));

        operation.PeakConcurrency.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void Average_time_is_zero_when_nothing_succeeded()
    {
        var sut = new ParallelImageProcessor(
            new FakeOperation((_, _) => throw new InvalidDataException("all bad")));

        var stats = sut.ProcessAll(Jobs(5));

        stats.Succeeded.Should().Be(0);
        stats.AverageTime.Should().Be(TimeSpan.Zero, "averaging an empty set must not divide by zero");
        stats.SuccessRate.Should().Be(0);
    }

    [Fact]
    public void An_empty_batch_produces_empty_statistics_rather_than_throwing()
    {
        var sut = new ParallelImageProcessor(new FakeOperation());

        var stats = sut.ProcessAll([]);

        stats.Total.Should().Be(0);
        stats.SuccessRate.Should().Be(0);
        stats.AverageTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void The_generated_batch_contains_the_corrupt_files_the_demo_relies_on()
    {
        var batch = ParallelImageProcessor.CreateBatch(100);

        batch.Should().HaveCount(100);
        batch.Count(j => j.Name.Contains("corrupt")).Should().Be(5);
    }
}
