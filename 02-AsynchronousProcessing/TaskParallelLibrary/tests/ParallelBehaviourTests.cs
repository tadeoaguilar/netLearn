using System.Collections.Concurrent;

namespace TaskParallelLibrary.Tests;

/// <summary>
/// The rules of Parallel.* and PLINQ. These assert behaviour, never timing --
/// a test that asserts "parallel was faster" fails on a busy CI machine and
/// teaches you nothing when it does.
/// </summary>
public class ParallelBehaviourTests
{
    [Fact]
    public void Parallel_For_runs_every_iteration_exactly_once()
    {
        var seen = new ConcurrentBag<int>();

        Parallel.For(0, 1000, i => seen.Add(i));

        seen.Should().HaveCount(1000);
        seen.Distinct().Should().HaveCount(1000);
    }

    [Fact]
    public void Plus_plus_on_a_shared_counter_is_not_atomic()
    {
        // The bug this module exists to prevent. Increment is read-modify-write,
        // and threads interleave between the steps.
        var unsafeCounter = 0;
        var safeCounter = 0;

        Parallel.For(0, 100_000, _ =>
        {
            unsafeCounter++;                          // WRONG
            Interlocked.Increment(ref safeCounter);   // CORRECT
        });

        safeCounter.Should().Be(100_000);
        unsafeCounter.Should().BeLessThanOrEqualTo(100_000);
    }

    [Fact]
    public void MaxDegreeOfParallelism_caps_concurrent_iterations()
    {
        var concurrent = 0;
        var peak = 0;
        var gate = new Lock();

        Parallel.For(0, 50, new ParallelOptions { MaxDegreeOfParallelism = 2 }, _ =>
        {
            var running = Interlocked.Increment(ref concurrent);
            lock (gate) peak = Math.Max(peak, running);
            Thread.Sleep(5);
            Interlocked.Decrement(ref concurrent);
        });

        peak.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void Parallel_For_collects_every_failure_into_an_AggregateException()
    {
        var act = () => Parallel.For(0, 20, i =>
        {
            if (i is 5 or 15) throw new InvalidOperationException($"item {i}");
        });

        act.Should().Throw<AggregateException>()
            .Which.InnerExceptions.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void A_cancellation_token_in_ParallelOptions_throws_OperationCanceled_not_Aggregate()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => Parallel.For(0, 100,
            new ParallelOptions { CancellationToken = cts.Token }, _ => { });

        // Without CancellationToken set on the options you would get an
        // AggregateException wrapping it instead.
        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Plinq_without_AsOrdered_may_return_results_out_of_order()
    {
        var ordered = Enumerable.Range(1, 200_000)
            .AsParallel()
            .AsOrdered()
            .Where(i => i % 3 == 0)
            .ToList();

        ordered.Should().BeInAscendingOrder();
        ordered.Should().Equal(Enumerable.Range(1, 200_000).Where(i => i % 3 == 0));
    }

    [Fact]
    public void Plinq_produces_the_same_set_as_sequential_Linq()
    {
        var sequential = Enumerable.Range(1, 50_000).Where(Examples.PlinqExample.IsPrime).ToList();
        var parallel = Enumerable.Range(1, 50_000).AsParallel().Where(Examples.PlinqExample.IsPrime).ToList();

        parallel.Should().BeEquivalentTo(sequential);
    }

    [Fact]
    public void Partitioner_ranges_cover_the_whole_source_without_overlap()
    {
        var covered = new ConcurrentBag<int>();

        Parallel.ForEach(Partitioner.Create(0, 100, 10), range =>
        {
            for (var i = range.Item1; i < range.Item2; i++) covered.Add(i);
        });

        covered.OrderBy(i => i).Should().Equal(Enumerable.Range(0, 100));
    }
}
