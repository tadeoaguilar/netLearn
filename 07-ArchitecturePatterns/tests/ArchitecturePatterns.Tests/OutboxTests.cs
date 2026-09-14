using Outbox.Relay;
using Outbox.Store;

namespace ArchitecturePatterns.Tests;

public class OutboxTests
{
    [Fact]
    public void The_dual_write_problem_loses_the_message()
    {
        // The bug the pattern exists to fix, demonstrated rather than asserted
        // in prose.
        var store = new TransactionalStore();
        var published = new List<string>();

        var act = () => store.SaveOrderThenPublish(
            new Order("O-1", "c-1", 50m), () => published.Add("OrderPlaced"), crashAfterSave: true);

        act.Should().Throw<InvalidOperationException>();
        store.Orders.Should().ContainSingle("the order was committed");
        published.Should().BeEmpty("and nobody was ever told");
    }

    [Fact]
    public void An_outbox_write_is_all_or_nothing()
    {
        var store = new TransactionalStore();

        var act = () => store.SaveOrderWithOutbox(
            new Order("O-1", "c-1", 50m), "OrderPlaced", "O-1", crashDuringCommit: true);

        act.Should().Throw<InvalidOperationException>();
        store.Orders.Should().BeEmpty();
        store.Outbox.Should().BeEmpty("neither was written, so there is nothing to reconcile");
    }

    [Fact]
    public void A_successful_commit_stores_the_order_and_its_message_together()
    {
        var store = new TransactionalStore();

        store.SaveOrderWithOutbox(new Order("O-1", "c-1", 50m), "OrderPlaced", "O-1");

        store.Orders.Should().ContainSingle();
        store.Outbox.Should().ContainSingle();
        store.UnpublishedMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task The_relay_publishes_pending_messages_exactly_once()
    {
        var store = new TransactionalStore();
        var published = new List<string>();

        for (var i = 1; i <= 3; i++)
        {
            store.SaveOrderWithOutbox(new Order($"O-{i}", "c", 10m), "OrderPlaced", $"O-{i}");
        }

        var relay = new OutboxRelay(store, (m, _) => { published.Add(m.Payload); return Task.CompletedTask; });

        var first = await relay.PublishPendingAsync();
        var second = await relay.PublishPendingAsync();

        first.Published.Should().Be(3);
        second.Published.Should().Be(0, "already-published messages are not resent");
        published.Should().HaveCount(3);
        store.UnpublishedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Nothing_is_lost_while_the_broker_is_down()
    {
        var store = new TransactionalStore();
        store.SaveOrderWithOutbox(new Order("O-1", "c", 10m), "OrderPlaced", "O-1");

        var brokerUp = false;
        var relay = new OutboxRelay(store, (m, _) => brokerUp
            ? Task.CompletedTask
            : throw new InvalidOperationException("unreachable"));

        var down = await relay.PublishPendingAsync();
        down.Published.Should().Be(0);
        down.Failed.Should().Be(1);
        store.UnpublishedMessages.Should().ContainSingle("the outbox IS the retry queue");

        brokerUp = true;
        var up = await relay.PublishPendingAsync();

        up.Published.Should().Be(1);
        store.UnpublishedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task A_crash_between_publishing_and_marking_causes_a_duplicate_not_a_loss()
    {
        // Publishing and marking cannot be atomic either, so the outbox
        // guarantees at-least-once. Exactly-once is the consumer's job.
        var store = new TransactionalStore();
        store.SaveOrderWithOutbox(new Order("O-1", "c", 10m), "OrderPlaced", "O-1");

        var deliveries = new List<string>();

        var crashing = new OutboxRelay(store, (m, _) =>
        {
            deliveries.Add(m.Payload);
            throw new InvalidOperationException("crashed after publish, before mark");
        });
        await crashing.PublishPendingAsync();

        var working = new OutboxRelay(store, (m, _) => { deliveries.Add(m.Payload); return Task.CompletedTask; });
        await working.PublishPendingAsync();

        deliveries.Should().HaveCount(2, "the same message was delivered twice");
        deliveries.Should().AllBe("O-1");
        store.UnpublishedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task A_message_that_keeps_failing_is_abandoned_rather_than_retried_forever()
    {
        var store = new TransactionalStore();
        store.SaveOrderWithOutbox(new Order("O-1", "c", 10m), "OrderPlaced", "O-1");

        var relay = new OutboxRelay(store, (_, _) => throw new InvalidOperationException("bad"), maxAttempts: 3);

        for (var i = 0; i < 3; i++) await relay.PublishPendingAsync();
        var final = await relay.PublishPendingAsync();

        final.Abandoned.Should().Be(1);
        final.Failed.Should().Be(0, "it is no longer being attempted");
    }
}
