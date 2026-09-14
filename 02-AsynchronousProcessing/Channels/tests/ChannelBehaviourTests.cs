using System.Threading.Channels;

namespace Channels.Tests;

/// <summary>
/// The rules of System.Threading.Channels. Every one of these is a rule people
/// learn the hard way from a hung process.
/// </summary>
public class ChannelBehaviourTests
{
    [Fact]
    public async Task ReadAllAsync_ends_only_when_the_writer_is_completed()
    {
        var channel = Channel.CreateUnbounded<int>();
        await channel.Writer.WriteAsync(1);
        await channel.Writer.WriteAsync(2);
        channel.Writer.Complete();

        var received = new List<int>();
        await foreach (var item in channel.Reader.ReadAllAsync())
        {
            received.Add(item);
        }

        received.Should().Equal(new[] { 1, 2 });
    }

    [Fact]
    public async Task Without_Complete_the_reader_waits_forever()
    {
        // The single most common channel bug, pinned down. The loop below does
        // not end on its own -- only the timeout rescues it.
        var channel = Channel.CreateUnbounded<int>();
        await channel.Writer.WriteAsync(1);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var received = new List<int>();

        var act = async () =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cts.Token))
            {
                received.Add(item);
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        received.Should().Equal(new[] { 1 }, "the item was delivered; the loop just never ended");
    }

    [Fact]
    public async Task A_bounded_channel_blocks_the_writer_when_it_is_full()
    {
        var channel = Channel.CreateBounded<int>(2);
        await channel.Writer.WriteAsync(1);
        await channel.Writer.WriteAsync(2);

        // The third write cannot complete until something is read.
        var third = channel.Writer.WriteAsync(3).AsTask();
        third.IsCompleted.Should().BeFalse("capacity is 2 and nothing has been read");

        (await channel.Reader.ReadAsync()).Should().Be(1);
        await third;   // now it can proceed

        third.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void DropOldest_keeps_the_most_recent_items()
    {
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        for (var i = 1; i <= 10; i++)
        {
            channel.Writer.TryWrite(i).Should().BeTrue("a drop mode never rejects a write");
        }

        channel.Writer.Complete();

        var kept = new List<int>();
        while (channel.Reader.TryRead(out var item)) kept.Add(item);

        kept.Should().Equal(new[] { 8, 9, 10 });
    }

    [Fact]
    public void DropWrite_keeps_the_earliest_items()
    {
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        });

        for (var i = 1; i <= 10; i++) channel.Writer.TryWrite(i);
        channel.Writer.Complete();

        var kept = new List<int>();
        while (channel.Reader.TryRead(out var item)) kept.Add(item);

        kept.Should().Equal(new[] { 1, 2, 3 }, "the incoming item is discarded, not a queued one");
    }

    [Fact]
    public async Task The_readers_Completion_task_carries_the_original_exception_too()
    {
        var channel = Channel.CreateUnbounded<int>();
        channel.Writer.Complete(new InvalidOperationException("producer broke"));

        var act = async () => await channel.Reader.Completion;

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Complete_with_an_exception_faults_the_reader()
    {
        var channel = Channel.CreateUnbounded<int>();
        await channel.Writer.WriteAsync(1);
        channel.Writer.Complete(new InvalidOperationException("producer broke"));

        var received = new List<int>();

        var act = async () =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync()) received.Add(item);
        };

        // ReadAllAsync rethrows the ORIGINAL exception, not a ChannelClosedException
        // wrapping it. EXERCISE.md catches ChannelClosedException here, which
        // never fires -- see GETTING_STARTED.md.
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Be("producer broke");

        received.Should().Equal(new[] { 1 }, "items written before the failure still arrive");
    }

    [Fact]
    public async Task Writing_to_a_completed_channel_throws()
    {
        var channel = Channel.CreateUnbounded<int>();
        channel.Writer.Complete();

        var act = async () => await channel.Writer.WriteAsync(1);

        await act.Should().ThrowAsync<ChannelClosedException>();
        channel.Writer.TryWrite(1).Should().BeFalse("TryWrite reports it instead of throwing");
    }

    [Fact]
    public async Task Each_item_goes_to_exactly_one_of_several_consumers()
    {
        var channel = Channel.CreateUnbounded<int>();
        for (var i = 0; i < 100; i++) await channel.Writer.WriteAsync(i);
        channel.Writer.Complete();

        var received = new System.Collections.Concurrent.ConcurrentBag<int>();
        var consumers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync()) received.Add(item);
        }));

        await Task.WhenAll(consumers);

        // Load balancing, not broadcast: 100 items in, 100 out -- not 400.
        received.Should().HaveCount(100);
        received.Distinct().Should().HaveCount(100);
    }
}
