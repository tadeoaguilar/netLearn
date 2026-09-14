using MessageQueues.Broker;

namespace MessageQueues.Demos;

public static class Demos
{
    public static async Task Part1BasicQueue()
    {
        Console.WriteLine("=== PART 1: PUBLISH AND CONSUME ===\n");

        var broker = new InMemoryBroker();

        for (var i = 1; i <= 5; i++)
        {
            broker.Publish("orders", $"order-{i}");
        }

        Console.WriteLine($"Queue depth before draining: {broker.QueueDepth}\n");

        var result = await broker.DrainAsync((message, _) =>
        {
            Console.WriteLine($"  [consumer] {message.Topic}: {message.Body}");
            return Task.CompletedTask;
        });

        Console.WriteLine($"\nDelivered {result.Delivered}, queue depth now {broker.QueueDepth}");
        Console.WriteLine("\nThe producer never waited for the consumer. That decoupling");
        Console.WriteLine("is the entire reason to put a queue between two services.");
    }

    public static async Task Part2RetryAndDeadLetter()
    {
        Console.WriteLine("=== PART 2: RETRY AND DEAD-LETTERING ===\n");

        var broker = new InMemoryBroker(new BrokerOptions { MaxDeliveries = 3 });

        broker.Publish("orders", "good-1");
        broker.Publish("orders", "POISON");
        broker.Publish("orders", "good-2");

        var result = await broker.DrainAsync((message, _) =>
        {
            if (message.Body == "POISON")
            {
                Console.WriteLine($"  [consumer] attempt {message.DeliveryCount} on {message.Body} -- throwing");
                throw new InvalidOperationException("cannot parse payload");
            }

            Console.WriteLine($"  [consumer] handled {message.Body}");
            return Task.CompletedTask;
        });

        Console.WriteLine($"\nDelivered: {result.Delivered}, failed attempts: {result.Failed}");
        Console.WriteLine($"Dead letters: {result.DeadLettered}");

        foreach (var (message, reason) in broker.DeadLetters)
        {
            Console.WriteLine($"  [DLQ] {message.Body} after {message.DeliveryCount} attempts: {reason}");
        }

        Console.WriteLine("\nThe good messages were unaffected. Without a delivery limit,");
        Console.WriteLine("that one poison message would redeliver forever and block the queue.");
    }

    public static async Task Part3Idempotency()
    {
        Console.WriteLine("=== PART 3: IDEMPOTENT CONSUMERS ===\n");

        var charged = new List<string>();

        var consumer = new IdempotentConsumer((message, _) =>
        {
            charged.Add(message.Body);
            Console.WriteLine($"  [payment] charging for {message.Body}");
            return Task.CompletedTask;
        });

        var broker = new InMemoryBroker();

        // The same message id published three times -- exactly what a broker
        // redelivery or a retried publish looks like from the consumer's side.
        broker.Publish("payments", "invoice-42", id: "msg-1");
        broker.Publish("payments", "invoice-42", id: "msg-1");
        broker.Publish("payments", "invoice-42", id: "msg-1");
        broker.Publish("payments", "invoice-43", id: "msg-2");

        await broker.DrainAsync(consumer.HandleAsync);

        Console.WriteLine($"\nMessages delivered: 4");
        Console.WriteLine($"Charges actually made: {charged.Count}");
        Console.WriteLine($"Duplicates skipped: {consumer.DuplicatesSkipped}");
        Console.WriteLine("\nWithout the dedupe the customer is charged three times.");
        Console.WriteLine("At-least-once delivery makes this the consumer's problem, not the broker's.");
    }

    public static async Task Part4CompetingConsumers()
    {
        Console.WriteLine("=== PART 4: COMPETING CONSUMERS ===\n");

        var broker = new InMemoryBroker();
        for (var i = 1; i <= 12; i++) broker.Publish("work", $"job-{i}");

        var handled = new Dictionary<int, int>();
        var gate = new Lock();

        // Several workers drain the same queue. Each job goes to exactly one of
        // them -- the queue is load balancing, not broadcasting.
        var workers = Enumerable.Range(1, 3).Select(worker => Task.Run(async () =>
            await broker.DrainAsync(async (message, token) =>
            {
                await Task.Delay(Random.Shared.Next(5, 25), token);
                lock (gate) handled[worker] = handled.GetValueOrDefault(worker) + 1;
            })));

        await Task.WhenAll(workers);

        foreach (var (worker, count) in handled.OrderBy(kv => kv.Key))
        {
            Console.WriteLine($"  worker {worker} handled {count} job(s)");
        }

        Console.WriteLine($"\nTotal: {handled.Values.Sum()} of 12 -- each job done once.");
        Console.WriteLine("Scaling throughput means adding consumers, not a bigger machine.");
    }
}
