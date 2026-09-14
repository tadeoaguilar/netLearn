using System.Threading.Channels;

namespace Channels.Examples;

/// <summary>Part 3: fan-in and fan-out.</summary>
public class MultipleProducersConsumersExample
{
    public async Task MultipleProducers()
    {
        Console.WriteLine("=== MULTIPLE PRODUCERS (fan-in) ===\n");

        var channel = Channel.CreateUnbounded<string>();

        var producers = Enumerable.Range(1, 3)
            .Select(producerId => Task.Run(async () =>
            {
                for (int i = 1; i <= 5; i++)
                {
                    var message = $"Producer-{producerId}: Message-{i}";
                    await channel.Writer.WriteAsync(message);
                    await Task.Delay(Random.Shared.Next(50, 150));
                }
            }))
            .ToArray();

        var consumer = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var message in channel.Reader.ReadAllAsync())
            {
                count++;
                Console.WriteLine($"[Consumer] ({count,2}) {message}");
            }
            return count;
        });

        // Complete only after EVERY producer has finished. Calling Complete()
        // inside a producer would cut the others off mid-write.
        await Task.WhenAll(producers);
        channel.Writer.Complete();

        var total = await consumer;
        Console.WriteLine($"\nReceived {total} messages from 3 producers");
    }

    public async Task MultipleConsumers()
    {
        Console.WriteLine("\n=== MULTIPLE CONSUMERS (fan-out) ===\n");

        var channel = Channel.CreateUnbounded<int>();

        var producer = Task.Run(async () =>
        {
            for (int i = 1; i <= 20; i++)
            {
                await channel.Writer.WriteAsync(i);
                await Task.Delay(20);
            }

            channel.Writer.Complete();
        });

        // Every consumer reads from the same reader. Each item goes to exactly
        // one of them -- this is load balancing, not broadcast.
        var counts = new int[3];
        var consumers = Enumerable.Range(0, 3)
            .Select(index => Task.Run(async () =>
            {
                await foreach (var item in channel.Reader.ReadAllAsync())
                {
                    counts[index]++;
                    await Task.Delay(Random.Shared.Next(50, 150));
                }
            }))
            .ToArray();

        await producer;
        await Task.WhenAll(consumers);

        for (var i = 0; i < counts.Length; i++)
        {
            Console.WriteLine($"[Consumer {i + 1}] handled {counts[i]} items");
        }

        Console.WriteLine($"Total: {counts.Sum()} of 20 -- each item delivered once.");
        Console.WriteLine("\nThe split is uneven because consumers work at different");
        Console.WriteLine("speeds. That is the point: whoever is free takes the next item.");
    }
}
