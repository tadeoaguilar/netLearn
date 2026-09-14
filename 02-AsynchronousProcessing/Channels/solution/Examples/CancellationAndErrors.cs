using System.Threading.Channels;

namespace Channels.Examples;

/// <summary>Part 5: shutting a channel down, cleanly or otherwise.</summary>
public class CancellationAndErrorsExample
{
    public async Task DemonstrateCancellation()
    {
        Console.WriteLine("=== CHANNEL CANCELLATION ===\n");

        var channel = Channel.CreateUnbounded<int>();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        var producer = Task.Run(async () =>
        {
            try
            {
                for (int i = 1; i <= 100; i++)
                {
                    await channel.Writer.WriteAsync(i, cts.Token);
                    Console.WriteLine($"[Producer] Sent: {i}");
                    await Task.Delay(100, cts.Token);
                }

                channel.Writer.Complete();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[Producer] Cancelled");

                // Complete even on the cancellation path, or a consumer that
                // is not watching the same token would hang forever.
                channel.Writer.Complete();
            }
        });

        var consumer = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in channel.Reader.ReadAllAsync(cts.Token))
                {
                    Console.WriteLine($"[Consumer] Received: {item}");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[Consumer] Cancelled");
            }
        });

        await Task.WhenAll(producer, consumer);
        Console.WriteLine("\nCancellation handled gracefully");
    }

    public async Task DemonstrateErrorHandling()
    {
        Console.WriteLine("\n=== ERROR HANDLING ===\n");

        var channel = Channel.CreateUnbounded<int>();

        var producer = Task.Run(async () =>
        {
            try
            {
                for (int i = 1; i <= 10; i++)
                {
                    if (i == 5)
                    {
                        throw new InvalidOperationException("Producer failed!");
                    }

                    await channel.Writer.WriteAsync(i);
                    Console.WriteLine($"[Producer] Sent: {i}");
                    await Task.Delay(100);
                }

                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Producer] Error: {ex.Message}");

                // Complete(ex) tells consumers the stream ended BADLY. Plain
                // Complete() would look like an ordinary end of data, and the
                // failure would vanish.
                channel.Writer.Complete(ex);
            }
        });

        var consumer = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in channel.Reader.ReadAllAsync())
                {
                    Console.WriteLine($"[Consumer] Received: {item}");
                }
            }
            catch (InvalidOperationException ex)
            {
                // EXERCISE.md catches ChannelClosedException here. That never
                // fires: ReadAllAsync rethrows the ORIGINAL exception passed to
                // Complete(ex), not a ChannelClosedException wrapping it.
                // ChannelClosedException is what you get for WRITING to a
                // completed channel -- the opposite direction.
                Console.WriteLine($"[Consumer] Producer faulted: {ex.Message}");
                Console.WriteLine("The consumer got the items written before the failure,");
                Console.WriteLine("then learned that the producer broke rather than finished.");
            }
        });

        await Task.WhenAll(producer, consumer);
    }
}
