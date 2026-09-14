using System.Threading.Channels;

namespace Channels.Examples;

/// <summary>
/// Part 2: what happens when the producer outruns the consumer.
/// This is the whole reason to prefer a channel over a Queue.
/// </summary>
public class BoundedChannelExample
{
    public async Task DemonstrateBoundedChannel()
    {
        Console.WriteLine("=== BOUNDED CHANNEL (Capacity: 3) ===\n");

        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        var producer = Task.Run(async () =>
        {
            for (int i = 1; i <= 10; i++)
            {
                Console.WriteLine($"[Producer] Attempting to send: {i}");

                // This await is the backpressure. Once three items are queued
                // the producer simply stops here until the consumer drains one.
                await channel.Writer.WriteAsync(i);
                Console.WriteLine($"[Producer] Sent: {i}");
            }

            channel.Writer.Complete();
            Console.WriteLine("[Producer] Completed");
        });

        var consumer = Task.Run(async () =>
        {
            await Task.Delay(500); // Start late, so the buffer fills first

            await foreach (var item in channel.Reader.ReadAllAsync())
            {
                Console.WriteLine($"[Consumer] Processing: {item}");
                await Task.Delay(300);
            }

            Console.WriteLine("[Consumer] Completed");
        });

        await Task.WhenAll(producer, consumer);

        Console.WriteLine("\nAn unbounded channel here would have queued all ten");
        Console.WriteLine("immediately and grown without limit. Bounded capacity is how");
        Console.WriteLine("a slow consumer pushes back instead of running you out of memory.");
    }

    public async Task DemonstrateDropNewest()
    {
        Console.WriteLine("\n=== DROP NEWEST MODE ===\n");

        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.DropNewest
        });

        for (int i = 1; i <= 10; i++)
        {
            // TryWrite never blocks. With a drop mode it always succeeds --
            // something else silently went out of the buffer to make room.
            var written = channel.Writer.TryWrite(i);
            Console.WriteLine($"[Producer] {i}: {(written ? "accepted" : "rejected")}");
        }

        channel.Writer.Complete();

        var received = new List<int>();
        await foreach (var item in channel.Reader.ReadAllAsync())
        {
            received.Add(item);
        }

        Console.WriteLine($"[Consumer] Kept: {string.Join(", ", received)}");
        Console.WriteLine("DropNewest discards the item at the back of the queue,");
        Console.WriteLine("so the earliest arrivals survive.");
    }

    public async Task DemonstrateDropOldest()
    {
        Console.WriteLine("\n=== DROP OLDEST MODE ===\n");

        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        for (int i = 1; i <= 10; i++)
        {
            channel.Writer.TryWrite(i);
        }

        channel.Writer.Complete();

        var received = new List<int>();
        await foreach (var item in channel.Reader.ReadAllAsync())
        {
            received.Add(item);
        }

        Console.WriteLine($"[Consumer] Kept: {string.Join(", ", received)}");
        Console.WriteLine("DropOldest keeps the most recent items -- the right choice");
        Console.WriteLine("for telemetry or live metrics, where stale data is useless.");
    }
}
