using Channels.Challenge;

namespace Channels.Tests;

/// <summary>The challenge: every requirement from EXERCISE.md, checked.</summary>
public class MessagePipelineTests
{
    private sealed class AcceptAll : IMessageValidator
    {
        public bool TryValidate(RawMessage message, out string? error)
        {
            error = null;
            return true;
        }
    }

    private sealed class RejectIds : IMessageValidator
    {
        private readonly HashSet<int> _rejected;
        public RejectIds(params int[] ids) => _rejected = [.. ids];

        public bool TryValidate(RawMessage message, out string? error)
        {
            if (_rejected.Contains(message.Id))
            {
                error = "rejected by test";
                return false;
            }
            error = null;
            return true;
        }
    }

    private sealed class PassThrough : IMessageTransformer
    {
        public int Transformed;

        public ValueTask<ProcessedMessage> TransformAsync(
            ValidMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Transformed);
            return ValueTask.FromResult(new ProcessedMessage(message.Id, message.Payload));
        }
    }

    private static MessageProcessingSystem Build(
        IMessageValidator validator,
        IMessageTransformer transformer,
        IMessageStore store,
        int capacity = 10,
        int processors = 3)
        => new(validator, transformer, store,
            new PipelineOptions { Capacity = capacity, ProcessorCount = processors });

    private static IReadOnlyList<RawMessage> Messages(int count) =>
        Enumerable.Range(1, count).Select(i => new RawMessage(i, $"payload-{i}")).ToList();

    [Fact]
    public async Task Every_valid_message_reaches_storage()
    {
        var store = new InMemoryMessageStore(TimeSpan.Zero);
        var sut = Build(new AcceptAll(), new PassThrough(), store);

        var stats = await sut.RunAsync(Messages(100));

        stats.Produced.Should().Be(100);
        stats.Validated.Should().Be(100);
        stats.Processed.Should().Be(100);
        stats.Stored.Should().Be(100);
        store.Saved.Should().HaveCount(100);
        store.Saved.Select(m => m.Id).Distinct().Should().HaveCount(100);
    }

    [Fact]
    public async Task Invalid_messages_are_counted_and_never_reach_the_processor()
    {
        var transformer = new PassThrough();
        var store = new InMemoryMessageStore(TimeSpan.Zero);
        var sut = Build(new RejectIds(3, 7, 11), transformer, store);

        var stats = await sut.RunAsync(Messages(20));

        stats.FailedValidation.Should().Be(3);
        stats.Validated.Should().Be(17);
        transformer.Transformed.Should().Be(17, "rejected messages stop at the validator");
        store.Saved.Select(m => m.Id).Should().NotContain([3, 7, 11]);
    }

    [Fact]
    public async Task Validation_errors_are_reported_with_their_message_id()
    {
        var sut = Build(new RejectIds(5), new PassThrough(), new InMemoryMessageStore(TimeSpan.Zero));

        var stats = await sut.RunAsync(Messages(10));

        stats.ValidationErrors.Should().ContainSingle()
            .Which.Should().Be("#5: rejected by test");
    }

    [Fact]
    public async Task The_counts_are_internally_consistent()
    {
        var store = new InMemoryMessageStore(TimeSpan.Zero);
        var sut = Build(new EveryTenthFailsValidator(), new PassThrough(), store);

        var stats = await sut.RunAsync(Messages(100));

        (stats.Validated + stats.FailedValidation).Should().Be(stats.Produced);
        stats.Processed.Should().Be(stats.Validated);
        stats.Stored.Should().Be(stats.Processed);
        stats.ValidationRate.Should().BeApproximately(0.9, 0.001);
    }

    [Fact]
    public async Task All_three_processors_share_the_work()
    {
        var threads = new System.Collections.Concurrent.ConcurrentBag<int>();
        var transformer = new DelegateTransformer(async (message, token) =>
        {
            threads.Add(Environment.CurrentManagedThreadId);
            await Task.Delay(5, token);
            return new ProcessedMessage(message.Id, message.Payload);
        });
        var sut = Build(new AcceptAll(), transformer, new InMemoryMessageStore(TimeSpan.Zero));

        await sut.RunAsync(Messages(60));

        threads.Distinct().Should().HaveCountGreaterThan(1,
            "three processors run concurrently, not one after another");
    }

    [Fact]
    public async Task A_bounded_pipeline_never_buffers_more_than_its_capacity()
    {
        // Storage is slow and the producer is fast. With bounded channels the
        // producer is forced to wait rather than queueing all 200 messages.
        var inFlight = 0;
        var peak = 0;
        var gate = new Lock();

        var transformer = new DelegateTransformer(async (message, token) =>
        {
            var current = Interlocked.Increment(ref inFlight);
            lock (gate) peak = Math.Max(peak, current);
            await Task.Delay(2, token);
            Interlocked.Decrement(ref inFlight);
            return new ProcessedMessage(message.Id, message.Payload);
        });

        var sut = Build(new AcceptAll(), transformer, new InMemoryMessageStore(TimeSpan.FromMilliseconds(2)),
            capacity: 5, processors: 3);

        var stats = await sut.RunAsync(Messages(200));

        stats.Stored.Should().Be(200);
        peak.Should().BeLessThanOrEqualTo(3, "at most one message per processor is in flight");
    }

    [Fact]
    public async Task Cancellation_stops_the_pipeline()
    {
        using var cts = new CancellationTokenSource();
        var transformer = new DelegateTransformer(async (message, token) =>
        {
            if (message.Id == 10) await cts.CancelAsync();
            await Task.Delay(5, token);
            return new ProcessedMessage(message.Id, message.Payload);
        });
        var store = new InMemoryMessageStore(TimeSpan.Zero);
        var sut = Build(new AcceptAll(), transformer, store, processors: 1);

        var act = () => sut.RunAsync(Messages(500), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        store.Saved.Should().HaveCountLessThan(500, "the pipeline stopped early");
    }

    [Fact]
    public async Task An_empty_batch_completes_without_hanging()
    {
        var sut = Build(new AcceptAll(), new PassThrough(), new InMemoryMessageStore(TimeSpan.Zero));

        var stats = await sut.RunAsync([]);

        stats.Produced.Should().Be(0);
        stats.Stored.Should().Be(0);
        stats.ValidationRate.Should().Be(0);
    }

    [Fact]
    public async Task A_batch_where_everything_fails_validation_still_completes()
    {
        var transformer = new PassThrough();
        var sut = Build(new RejectIds([.. Enumerable.Range(1, 20)]), transformer,
            new InMemoryMessageStore(TimeSpan.Zero));

        var stats = await sut.RunAsync(Messages(20));

        stats.FailedValidation.Should().Be(20);
        stats.Stored.Should().Be(0);
        transformer.Transformed.Should().Be(0);
    }

    private sealed class DelegateTransformer : IMessageTransformer
    {
        private readonly Func<ValidMessage, CancellationToken, Task<ProcessedMessage>> _behaviour;

        public DelegateTransformer(Func<ValidMessage, CancellationToken, Task<ProcessedMessage>> behaviour)
            => _behaviour = behaviour;

        public async ValueTask<ProcessedMessage> TransformAsync(
            ValidMessage message, CancellationToken cancellationToken)
            => await _behaviour(message, cancellationToken);
    }
}
