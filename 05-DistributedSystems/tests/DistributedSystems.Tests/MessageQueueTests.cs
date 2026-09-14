using MessageQueues.Broker;

namespace DistributedSystems.Tests;

public class MessageQueueTests
{
    [Fact]
    public async Task Every_published_message_is_delivered_once()
    {
        var broker = new InMemoryBroker();
        var received = new List<string>();

        for (var i = 1; i <= 5; i++) broker.Publish("orders", $"order-{i}");

        var result = await broker.DrainAsync((m, _) => { received.Add(m.Body); return Task.CompletedTask; });

        result.Delivered.Should().Be(5);
        received.Should().HaveCount(5).And.OnlyHaveUniqueItems();
        broker.QueueDepth.Should().Be(0);
    }

    [Fact]
    public async Task A_failing_handler_causes_redelivery_not_loss()
    {
        // At-least-once: the message must survive a handler that throws.
        var broker = new InMemoryBroker(new BrokerOptions { MaxDeliveries = 3 });
        broker.Publish("orders", "flaky", id: "m1");
        var attempts = 0;

        await broker.DrainAsync((m, _) =>
        {
            attempts++;
            if (attempts < 3) throw new InvalidOperationException("transient");
            return Task.CompletedTask;
        });

        attempts.Should().Be(3);
        broker.DeadLetters.Should().BeEmpty("it succeeded before the limit");
    }

    [Fact]
    public async Task A_poison_message_is_dead_lettered_after_the_delivery_limit()
    {
        var broker = new InMemoryBroker(new BrokerOptions { MaxDeliveries = 3 });
        broker.Publish("orders", "poison", id: "m1");

        var result = await broker.DrainAsync((_, _) => throw new InvalidOperationException("bad payload"));

        result.DeadLettered.Should().Be(1);
        result.Failed.Should().Be(3, "three attempts, then give up");
        broker.DeadLetters.Single().Message.DeliveryCount.Should().Be(3);
        broker.DeadLetters.Single().Reason.Should().Be("bad payload");
    }

    [Fact]
    public async Task A_poison_message_does_not_block_healthy_ones()
    {
        // Without a dead-letter queue this is head-of-line blocking: one bad
        // message stalls everything behind it, forever.
        var broker = new InMemoryBroker(new BrokerOptions { MaxDeliveries = 2 });
        var handled = new List<string>();

        broker.Publish("orders", "good-1");
        broker.Publish("orders", "POISON");
        broker.Publish("orders", "good-2");

        await broker.DrainAsync((m, _) =>
        {
            if (m.Body == "POISON") throw new InvalidOperationException("nope");
            handled.Add(m.Body);
            return Task.CompletedTask;
        });

        handled.Should().BeEquivalentTo(["good-1", "good-2"]);
        broker.DeadLetters.Should().ContainSingle();
    }

    [Fact]
    public async Task An_idempotent_consumer_processes_a_duplicate_only_once()
    {
        var work = new List<string>();
        var consumer = new IdempotentConsumer((m, _) => { work.Add(m.Body); return Task.CompletedTask; });
        var broker = new InMemoryBroker();

        broker.Publish("payments", "invoice-42", id: "msg-1");
        broker.Publish("payments", "invoice-42", id: "msg-1");
        broker.Publish("payments", "invoice-42", id: "msg-1");

        await broker.DrainAsync(consumer.HandleAsync);

        work.Should().ContainSingle("the customer must be charged exactly once");
        consumer.DuplicatesSkipped.Should().Be(2);
    }

    [Fact]
    public async Task A_failed_message_is_not_remembered_as_processed()
    {
        // If the dedupe recorded the id before the work succeeded, the retry
        // would be skipped and the message silently lost.
        var attempts = 0;
        var consumer = new IdempotentConsumer((m, _) =>
        {
            attempts++;
            if (attempts == 1) throw new InvalidOperationException("transient");
            return Task.CompletedTask;
        });
        var broker = new InMemoryBroker(new BrokerOptions { MaxDeliveries = 3 });

        broker.Publish("payments", "invoice-1", id: "msg-1");
        await broker.DrainAsync(consumer.HandleAsync);

        attempts.Should().Be(2, "the retry must actually run");
        consumer.Processed.Should().Be(1);
        broker.DeadLetters.Should().BeEmpty();
    }

    [Fact]
    public async Task Competing_consumers_split_the_work_without_duplicating_it()
    {
        var broker = new InMemoryBroker();
        for (var i = 0; i < 30; i++) broker.Publish("work", $"job-{i}");

        var handled = new System.Collections.Concurrent.ConcurrentBag<string>();
        var workers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
            await broker.DrainAsync(async (m, token) =>
            {
                await Task.Delay(1, token);
                handled.Add(m.Body);
            })));

        await Task.WhenAll(workers);

        handled.Should().HaveCount(30);
        handled.Distinct().Should().HaveCount(30, "a queue load balances, it does not broadcast");
    }
}
