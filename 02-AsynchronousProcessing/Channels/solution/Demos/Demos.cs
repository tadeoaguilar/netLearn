using Channels.Challenge;
using Channels.Examples;

namespace Channels.Demos;

public static class Demos
{
    public static Task Part1Basic() => new BasicChannelExample().SimpleProducerConsumer();

    public static async Task Part2Bounded()
    {
        var demo = new BoundedChannelExample();
        await demo.DemonstrateBoundedChannel();
        await demo.DemonstrateDropNewest();
        await demo.DemonstrateDropOldest();
    }

    public static async Task Part3MultipleProducersConsumers()
    {
        var demo = new MultipleProducersConsumersExample();
        await demo.MultipleProducers();
        await demo.MultipleConsumers();
    }

    public static Task Part4Pipeline() => new PipelineExample().ThreeStagesPipeline();

    public static async Task Part5CancellationAndErrors()
    {
        var demo = new CancellationAndErrorsExample();
        await demo.DemonstrateCancellation();
        await demo.DemonstrateErrorHandling();
    }

    public static Task Part6LogProcessor() => new LogProcessor().DemoLogProcessing();

    public static async Task ChallengeMessagePipeline()
    {
        Console.WriteLine("=== CHALLENGE: MESSAGE PROCESSING SYSTEM ===\n");

        var store = new InMemoryMessageStore();
        var system = new MessageProcessingSystem(
            new EveryTenthFailsValidator(),
            new UppercaseTransformer(),
            store,
            new PipelineOptions { Capacity = 10, ProcessorCount = 3 });

        var messages = Enumerable.Range(1, 100)
            .Select(i => new RawMessage(i, $"message-payload-{i}"))
            .ToList();

        Console.WriteLine("producer -> [raw] -> validator -> [valid] -> 3 processors -> [done] -> storage");
        Console.WriteLine("All channels bounded at 10.\n");

        var stats = await system.RunAsync(messages);

        Console.WriteLine($"--- Statistics ---");
        Console.WriteLine($"  Produced:           {stats.Produced}");
        Console.WriteLine($"  Validated:          {stats.Validated}");
        Console.WriteLine($"  Failed validation:  {stats.FailedValidation}");
        Console.WriteLine($"  Processed:          {stats.Processed}");
        Console.WriteLine($"  Stored:             {stats.Stored}");
        Console.WriteLine($"  Validation rate:    {stats.ValidationRate:P0}");
        Console.WriteLine($"  Elapsed:            {stats.Elapsed.TotalMilliseconds:N0}ms");

        Console.WriteLine($"\n  First few rejections:");
        foreach (var error in stats.ValidationErrors.OrderBy(e => e).Take(3))
        {
            Console.WriteLine($"    {error}");
        }

        var serial = 100 * 20 + 90 * 5; // transform cost + storage cost
        Console.WriteLine($"\n  Doing this one message at a time would take roughly {serial}ms.");
        Console.WriteLine($"  The pipeline overlapped every stage and took {stats.Elapsed.TotalMilliseconds:N0}ms.");
        Console.WriteLine($"  Nothing was ever queued more than 10 deep.");
    }
}
