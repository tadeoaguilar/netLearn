using System.Threading.Channels;

namespace Channels.Examples;

/// <summary>
/// Part 4: chaining channels so each stage runs concurrently with the others.
/// </summary>
public class PipelineExample
{
    public record DataItem(int Id, string Data);
    public record ProcessedItem(int Id, string Data, DateTime ProcessedAt);
    public record EnrichedItem(int Id, string Data, DateTime ProcessedAt, string Extra);

    public async Task ThreeStagesPipeline()
    {
        Console.WriteLine("=== THREE-STAGE PIPELINE ===\n");

        var channel1 = Channel.CreateBounded<DataItem>(10);
        var channel2 = Channel.CreateBounded<ProcessedItem>(10);
        var channel3 = Channel.CreateBounded<EnrichedItem>(10);

        // Stage 1: generate
        var stage1 = Task.Run(async () =>
        {
            for (int i = 1; i <= 10; i++)
            {
                await channel1.Writer.WriteAsync(new DataItem(i, $"Data-{i}"));
                Console.WriteLine($"[Stage 1] Generated: {i}");
                await Task.Delay(100);
            }

            channel1.Writer.Complete();
        });

        // Stage 2: process
        var stage2 = Task.Run(async () =>
        {
            await foreach (var item in channel1.Reader.ReadAllAsync())
            {
                await Task.Delay(50);
                var processed = new ProcessedItem(item.Id, item.Data.ToUpperInvariant(), DateTime.UtcNow);
                await channel2.Writer.WriteAsync(processed);
                Console.WriteLine($"[Stage 2] Processed: {item.Id}");
            }

            // Each stage completes its OWN output channel, once its input has
            // run dry. That is how completion propagates down the pipeline.
            channel2.Writer.Complete();
        });

        // Stage 3: enrich
        var stage3 = Task.Run(async () =>
        {
            await foreach (var item in channel2.Reader.ReadAllAsync())
            {
                await Task.Delay(30);
                var enriched = new EnrichedItem(item.Id, item.Data, item.ProcessedAt, $"meta-{item.Id}");
                await channel3.Writer.WriteAsync(enriched);
                Console.WriteLine($"[Stage 3] Enriched: {item.Id}");
            }

            channel3.Writer.Complete();
        });

        // Sink
        var sink = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var item in channel3.Reader.ReadAllAsync())
            {
                count++;
                Console.WriteLine($"[Output] {item.Id}: {item.Data} ({item.Extra})");
            }
            return count;
        });

        await Task.WhenAll(stage1, stage2, stage3);
        var total = await sink;

        Console.WriteLine($"\n{total} items completed the pipeline.");
        Console.WriteLine("All four stages ran at once: stage 1 was generating item 7");
        Console.WriteLine("while stage 3 was still enriching item 4.");
    }
}
