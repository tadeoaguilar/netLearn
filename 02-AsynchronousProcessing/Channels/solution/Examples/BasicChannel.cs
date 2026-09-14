using System.Threading.Channels;

namespace Channels.Examples;

/// <summary>Part 1: one producer, one consumer, one channel.</summary>
public class BasicChannelExample
{
    public async Task SimpleProducerConsumer()
    {
        Console.WriteLine("=== BASIC PRODUCER-CONSUMER ===\n");

        var channel = Channel.CreateUnbounded<string>();

        var producer = Task.Run(async () =>
        {
            for (int i = 1; i <= 10; i++)
            {
                var message = $"Message {i}";
                await channel.Writer.WriteAsync(message);
                Console.WriteLine($"[Producer] Sent: {message}");
                await Task.Delay(100);
            }

            // Without this the consumer's await foreach never ends -- it waits
            // forever for a message that is not coming. Completing the writer
            // is what terminates the loop.
            channel.Writer.Complete();
            Console.WriteLine("[Producer] Completed");
        });

        var consumer = Task.Run(async () =>
        {
            await foreach (var message in channel.Reader.ReadAllAsync())
            {
                Console.WriteLine($"[Consumer] Received: {message}");
                await Task.Delay(50);
            }

            Console.WriteLine("[Consumer] Completed");
        });

        await Task.WhenAll(producer, consumer);
    }
}
