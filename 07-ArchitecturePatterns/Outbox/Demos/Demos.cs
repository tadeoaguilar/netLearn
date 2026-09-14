using Outbox.Relay;
using Outbox.Store;

namespace Outbox.Demos;

public static class Demos
{
    public static Task Part1TheDualWriteProblem()
    {
        Console.WriteLine("=== PART 1: THE DUAL-WRITE PROBLEM ===\n");

        var store = new TransactionalStore();
        var published = new List<string>();

        Console.WriteLine("Save the order, then publish the event. Process dies in between:\n");

        try
        {
            store.SaveOrderThenPublish(
                new Order("ORD-1", "cust-1", 50m),
                () => published.Add("OrderPlaced ORD-1"),
                crashAfterSave: true);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  crash: {ex.Message}");
        }

        Console.WriteLine($"\n  orders in database: {store.Orders.Count}");
        Console.WriteLine($"  events published:   {published.Count}");
        Console.WriteLine("\nThe order exists and nobody downstream will ever hear about it.");
        Console.WriteLine("No retry can fix this: the code that would have retried is gone,");
        Console.WriteLine("and nothing recorded that the publish was still owed.");
        return Task.CompletedTask;
    }

    public static Task Part2OutboxSurvivesTheCrash()
    {
        Console.WriteLine("=== PART 2: ONE TRANSACTION, TWO WRITES ===\n");

        var store = new TransactionalStore();

        Console.WriteLine("Crash DURING the commit:\n");
        try
        {
            store.SaveOrderWithOutbox(new Order("ORD-2", "cust-2", 75m),
                "OrderPlaced", "ORD-2", crashDuringCommit: true);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  {ex.Message}");
        }

        Console.WriteLine($"  orders: {store.Orders.Count}, outbox: {store.Outbox.Count}");
        Console.WriteLine("  Neither was written. There is no inconsistent state to repair.\n");

        Console.WriteLine("Now a successful commit:\n");
        store.SaveOrderWithOutbox(new Order("ORD-3", "cust-3", 75m), "OrderPlaced", "ORD-3");

        Console.WriteLine($"  orders: {store.Orders.Count}, outbox: {store.Outbox.Count}");
        Console.WriteLine("  Both written together. The event is now a durable obligation.");
        return Task.CompletedTask;
    }

    public static async Task Part3TheRelay()
    {
        Console.WriteLine("=== PART 3: THE RELAY PUBLISHES LATER ===\n");

        var store = new TransactionalStore();
        var published = new List<string>();

        for (var i = 1; i <= 3; i++)
        {
            store.SaveOrderWithOutbox(new Order($"ORD-{i}", $"cust-{i}", i * 10m), "OrderPlaced", $"ORD-{i}");
        }

        Console.WriteLine($"Pending in outbox: {store.UnpublishedMessages.Count}\n");

        var relay = new OutboxRelay(store, (message, _) =>
        {
            published.Add(message.Payload);
            Console.WriteLine($"  [relay] published {message.Type} {message.Payload}");
            return Task.CompletedTask;
        });

        var result = await relay.PublishPendingAsync();

        Console.WriteLine($"\nPublished {result.Published}, still pending {store.UnpublishedMessages.Count}");

        // Running again must publish nothing -- the messages are marked.
        var second = await relay.PublishPendingAsync();
        Console.WriteLine($"Second run published {second.Published} (nothing left to do)");
        return;
    }

    public static async Task Part4BrokerDownThenRecovering()
    {
        Console.WriteLine("=== PART 4: THE BROKER IS DOWN ===\n");

        var store = new TransactionalStore();
        for (var i = 1; i <= 3; i++)
        {
            store.SaveOrderWithOutbox(new Order($"ORD-{i}", $"cust-{i}", 10m), "OrderPlaced", $"ORD-{i}");
        }

        var brokerUp = false;
        var published = new List<string>();

        var relay = new OutboxRelay(store, (message, _) =>
        {
            if (!brokerUp) throw new InvalidOperationException("broker unreachable");
            published.Add(message.Payload);
            return Task.CompletedTask;
        });

        var down = await relay.PublishPendingAsync();
        Console.WriteLine($"Broker down -> published {down.Published}, failed {down.Failed}");
        Console.WriteLine($"  outbox still holds {store.UnpublishedMessages.Count} messages\n");

        Console.WriteLine("Broker comes back:\n");
        brokerUp = true;
        var up = await relay.PublishPendingAsync();

        Console.WriteLine($"  published {up.Published}, pending now {store.UnpublishedMessages.Count}");
        Console.WriteLine("\nNothing was lost while the broker was unavailable. The outbox IS");
        Console.WriteLine("the retry queue, and it is as durable as the order itself.");
    }

    public static async Task Part5AtLeastOnce()
    {
        Console.WriteLine("=== PART 5: WHY CONSUMERS MUST BE IDEMPOTENT ===\n");

        var store = new TransactionalStore();
        store.SaveOrderWithOutbox(new Order("ORD-1", "cust-1", 100m), "OrderPlaced", "ORD-1");

        var deliveries = new List<string>();

        // The relay publishes, then marks. A crash in between means the next
        // run publishes the same message again.
        var flakyRelay = new OutboxRelay(store, (message, _) =>
        {
            deliveries.Add(message.Payload);
            throw new InvalidOperationException("crashed after publishing, before marking");
        });

        await flakyRelay.PublishPendingAsync();
        Console.WriteLine($"  attempt 1: delivered {deliveries.Count}, still marked unpublished");

        var goodRelay = new OutboxRelay(store, (message, _) =>
        {
            deliveries.Add(message.Payload);
            return Task.CompletedTask;
        });

        await goodRelay.PublishPendingAsync();
        Console.WriteLine($"  attempt 2: delivered {deliveries.Count} total");

        Console.WriteLine($"\nThe consumer saw ORD-1 {deliveries.Count} times for one order.");
        Console.WriteLine("This is unavoidable: publishing and marking cannot be atomic either.");
        Console.WriteLine("The outbox guarantees at-least-once. Exactly-once is achieved at the");
        Console.WriteLine("CONSUMER, by deduplicating on message id -- see 05-DistributedSystems.");
    }
}
