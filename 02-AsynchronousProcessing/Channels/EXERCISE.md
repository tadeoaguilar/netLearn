# Exercise: System.Threading.Channels

## Overview
Master producer-consumer patterns using System.Threading.Channels, a modern high-performance alternative to BlockingCollection for asynchronous workflows.

## Learning Goals
- Understand channel concepts
- Implement producer-consumer patterns
- Handle bounded vs unbounded channels
- Manage backpressure
- Build processing pipelines
- Use multiple producers and consumers

---

## Part 1: Basic Channel Usage

### Step 1.1: Simple Producer-Consumer

**Your Task:** Create `Examples/BasicChannel.cs`:

```csharp
using System.Threading.Channels;

namespace Channels.Examples;

public class BasicChannelExample
{
    public async Task SimpleProducerConsumer()
    {
        Console.WriteLine("=== BASIC PRODUCER-CONSUMER ===\n");

        // Create an unbounded channel
        var channel = Channel.CreateUnbounded<string>();

        // Producer task
        var producer = Task.Run(async () =>
        {
            for (int i = 1; i <= 10; i++)
            {
                var message = $"Message {i}";
                await channel.Writer.WriteAsync(message);
                Console.WriteLine($"[Producer] Sent: {message}");
                await Task.Delay(100);
            }

            // Signal completion
            channel.Writer.Complete();
            Console.WriteLine("[Producer] Completed");
        });

        // Consumer task
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
```

**Key Concepts:**
- `Channel.CreateUnbounded<T>()`: No limit on messages
- `Writer.WriteAsync()`: Send message
- `Reader.ReadAllAsync()`: Consume all messages
- `Writer.Complete()`: Signal no more messages

---

## Part 2: Bounded Channels and Backpressure

### Step 2.1: Handling Backpressure

**Your Task:** Create `Examples/BoundedChannel.cs`:

```csharp
using System.Threading.Channels;

namespace Channels.Examples;

public class BoundedChannelExample
{
    public async Task DemonstrateBoundedChannel()
    {
        Console.WriteLine("=== BOUNDED CHANNEL (Capacity: 3) ===\n");

        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.Wait  // Block when full
        });

        // Fast producer
        var producer = Task.Run(async () =>
        {
            for (int i = 1; i <= 10; i++)
            {
                Console.WriteLine($"[Producer] Attempting to send: {i}");
                await channel.Writer.WriteAsync(i);
                Console.WriteLine($"[Producer] Sent: {i}");
            }

            channel.Writer.Complete();
            Console.WriteLine("[Producer] Completed");
        });

        // Slow consumer
        var consumer = Task.Run(async () =>
        {
            await Task.Delay(500); // Start delayed

            await foreach (var item in channel.Reader.ReadAllAsync())
            {
                Console.WriteLine($"[Consumer] Processing: {item}");
                await Task.Delay(300); // Slow processing
            }

            Console.WriteLine("[Consumer] Completed");
        });

        await Task.WhenAll(producer, consumer);
    }

    public async Task DemonstrateDropNewest()
    {
        Console.WriteLine("\n=== DROP NEWEST MODE ===\n");

        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.DropNewest
        });

        // Producer sends 10 items
        for (int i = 1; i <= 10; i++)
        {
            var written = channel.Writer.TryWrite(i);
            Console.WriteLine($"[Producer] {i}: {(written ? "Written" : "Dropped")}");
        }

        channel.Writer.Complete();

        // Consumer reads what's available
        await foreach (var item in channel.Reader.ReadAllAsync())
        {
            Console.WriteLine($"[Consumer] Received: {item}");
        }
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
            Console.WriteLine($"[Producer] Wrote: {i}");
        }

        channel.Writer.Complete();

        await foreach (var item in channel.Reader.ReadAllAsync())
        {
            Console.WriteLine($"[Consumer] Received: {item}");
        }
    }
}
```

**BoundedChannelFullMode Options:**
- `Wait`: Block producer until space available
- `DropWrite`: Drop incoming message
- `DropNewest`: Drop newest message in queue
- `DropOldest`: Drop oldest message in queue

---

## Part 3: Multiple Producers and Consumers

### Step 3.1: Multiple Writers

**Your Task:** Create `Examples/MultipleProducersConsumers.cs`:

```csharp
using System.Threading.Channels;

namespace Channels.Examples;

public class MultipleProducersConsumersExample
{
    public async Task MultipleProducers()
    {
        Console.WriteLine("=== MULTIPLE PRODUCERS ===\n");

        var channel = Channel.CreateUnbounded<string>();

        // Create 3 producers
        var producers = Enumerable.Range(1, 3)
            .Select(producerId => Task.Run(async () =>
            {
                for (int i = 1; i <= 5; i++)
                {
                    var message = $"Producer-{producerId}: Message-{i}";
                    await channel.Writer.WriteAsync(message);
                    Console.WriteLine($"[Producer {producerId}] Sent: {message}");
                    await Task.Delay(Random.Shared.Next(50, 150));
                }
            }))
            .ToArray();

        // Single consumer
        var consumer = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var message in channel.Reader.ReadAllAsync())
            {
                count++;
                Console.WriteLine($"[Consumer] Received ({count}): {message}");
            }
        });

        // Wait for all producers
        await Task.WhenAll(producers);
        channel.Writer.Complete();

        // Wait for consumer
        await consumer;

        Console.WriteLine("\nAll messages processed");
    }

    public async Task MultipleConsumers()
    {
        Console.WriteLine("\n=== MULTIPLE CONSUMERS ===\n");

        var channel = Channel.CreateUnbounded<int>();

        // Single producer
        var producer = Task.Run(async () =>
        {
            for (int i = 1; i <= 20; i++)
            {
                await channel.Writer.WriteAsync(i);
                Console.WriteLine($"[Producer] Sent: {i}");
                await Task.Delay(50);
            }

            channel.Writer.Complete();
        });

        // Create 3 consumers
        var consumers = Enumerable.Range(1, 3)
            .Select(consumerId => Task.Run(async () =>
            {
                await foreach (var item in channel.Reader.ReadAllAsync())
                {
                    Console.WriteLine($"[Consumer {consumerId}] Processing: {item}");
                    await Task.Delay(Random.Shared.Next(100, 300));
                }

                Console.WriteLine($"[Consumer {consumerId}] Finished");
            }))
            .ToArray();

        await Task.WhenAll(producer);
        await Task.WhenAll(consumers);

        Console.WriteLine("\nAll consumers finished");
    }
}
```

**Benefits:**
- Load balancing across consumers
- Parallel processing
- Fault tolerance (one consumer fails, others continue)

---

## Part 4: Processing Pipelines

### Step 4.1: Multi-Stage Pipeline

**Your Task:** Create `Examples/Pipeline.cs`:

```csharp
using System.Threading.Channels;

namespace Channels.Examples;

public class PipelineExample
{
    public record DataItem(int Id, string Data);
    public record ProcessedItem(int Id, string Data, DateTime ProcessedAt);
    public record EnrichedItem(int Id, string Data, DateTime ProcessedAt, string Extra);

    public async Task ThreeStagesPipeline()
    {
        Console.WriteLine("=== THREE-STAGE PIPELINE ===\n");

        // Stage 1 -> Stage 2
        var channel1 = Channel.CreateBounded<DataItem>(10);

        // Stage 2 -> Stage 3
        var channel2 = Channel.CreateBounded<ProcessedItem>(10);

        // Stage 3 -> Output
        var channel3 = Channel.CreateBounded<EnrichedItem>(10);

        // Stage 1: Data Generator
        var stage1 = Task.Run(async () =>
        {
            Console.WriteLine("[Stage 1] Starting data generation...");

            for (int i = 1; i <= 10; i++)
            {
                var item = new DataItem(i, $"Data-{i}");
                await channel1.Writer.WriteAsync(item);
                Console.WriteLine($"[Stage 1] Generated: {item.Id}");
                await Task.Delay(100);
            }

            channel1.Writer.Complete();
            Console.WriteLine("[Stage 1] Completed");
        });

        // Stage 2: Processor
        var stage2 = Task.Run(async () =>
        {
            Console.WriteLine("[Stage 2] Starting processing...");

            await foreach (var item in channel1.Reader.ReadAllAsync())
            {
                await Task.Delay(50); // Simulate processing

                var processed = new ProcessedItem(
                    item.Id,
                    item.Data.ToUpper(),
                    DateTime.UtcNow
                );

                await channel2.Writer.WriteAsync(processed);
                Console.WriteLine($"[Stage 2] Processed: {processed.Id}");
            }

            channel2.Writer.Complete();
            Console.WriteLine("[Stage 2] Completed");
        });

        // Stage 3: Enricher
        var stage3 = Task.Run(async () =>
        {
            Console.WriteLine("[Stage 3] Starting enrichment...");

            await foreach (var item in channel2.Reader.ReadAllAsync())
            {
                await Task.Delay(30); // Simulate enrichment

                var enriched = new EnrichedItem(
                    item.Id,
                    item.Data,
                    item.ProcessedAt,
                    $"Metadata-{item.Id}"
                );

                await channel3.Writer.WriteAsync(enriched);
                Console.WriteLine($"[Stage 3] Enriched: {enriched.Id}");
            }

            channel3.Writer.Complete();
            Console.WriteLine("[Stage 3] Completed");
        });

        // Final consumer
        var consumer = Task.Run(async () =>
        {
            var results = new List<EnrichedItem>();

            await foreach (var item in channel3.Reader.ReadAllAsync())
            {
                results.Add(item);
                Console.WriteLine($"[Consumer] Received final: {item.Id}");
            }

            Console.WriteLine($"\n✅ Pipeline completed! Processed {results.Count} items");
        });

        await Task.WhenAll(stage1, stage2, stage3, consumer);
    }
}
```

**Pipeline Pattern:**
```
[Generator] → Channel → [Processor] → Channel → [Enricher] → Channel → [Consumer]
```

---

## Part 5: Cancellation and Error Handling

### Step 5.1: Graceful Cancellation

**Your Task:** Create `Examples/CancellationAndErrors.cs`:

```csharp
using System.Threading.Channels;

namespace Channels.Examples;

public class CancellationAndErrorsExample
{
    public async Task DemonstrateCancellation()
    {
        Console.WriteLine("=== CHANNEL CANCELLATION ===\n");

        var channel = Channel.CreateUnbounded<int>();
        using var cts = new CancellationTokenSource();

        // Cancel after 2 seconds
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
                channel.Writer.Complete(ex); // Signal error to consumers
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
            catch (ChannelClosedException ex)
            {
                Console.WriteLine($"[Consumer] Channel closed: {ex.InnerException?.Message}");
            }
        });

        await Task.WhenAll(producer, consumer);
    }
}
```

---

## Part 6: Real-World Example - Log Processor

### Step 6.1: Build a Log Processing System

**Your Task:** Create `Examples/LogProcessor.cs`:

```csharp
using System.Threading.Channels;

namespace Channels.Examples;

public class LogProcessor
{
    public record LogEntry(DateTime Timestamp, string Level, string Message);

    private readonly Channel<LogEntry> _logChannel;
    private readonly List<LogEntry> _storedLogs = new();

    public LogProcessor(int bufferSize = 100)
    {
        _logChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(bufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest // Drop old logs if buffer full
        });
    }

    public void Log(string level, string message)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, message);

        if (_logChannel.Writer.TryWrite(entry))
        {
            Console.WriteLine($"[Logger] {level}: {message}");
        }
        else
        {
            Console.WriteLine($"[Logger] DROPPED: {level}: {message}");
        }
    }

    public async Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[Log Processor] Started");

        try
        {
            await foreach (var log in _logChannel.Reader.ReadAllAsync(cancellationToken))
            {
                // Simulate processing (write to file, database, etc.)
                await Task.Delay(10, cancellationToken);

                _storedLogs.Add(log);

                if (log.Level == "ERROR")
                {
                    Console.WriteLine($"[Log Processor] ⚠️ ERROR logged: {log.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Log Processor] Shutting down...");
        }

        Console.WriteLine($"[Log Processor] Processed {_storedLogs.Count} logs total");
    }

    public void Stop()
    {
        _logChannel.Writer.Complete();
    }

    public async Task DemoLogProcessing()
    {
        using var cts = new CancellationTokenSource();

        // Start processor
        var processorTask = StartProcessingAsync(cts.Token);

        // Simulate application logging
        var loggingTask = Task.Run(async () =>
        {
            for (int i = 1; i <= 20; i++)
            {
                var level = i % 5 == 0 ? "ERROR" : "INFO";
                Log(level, $"Application event {i}");
                await Task.Delay(50);
            }

            // Stop logging
            Stop();
        });

        await Task.WhenAll(loggingTask, processorTask);
    }
}
```

---

## Challenge Exercise

### Build a Message Processing System

**Requirements:**

1. **Three channels:**
   - Raw messages → Validator
   - Valid messages → Processor
   - Processed messages → Storage

2. **Components:**
   - **Producer**: Generates 100 messages
   - **Validator**: Validates messages (10% fail)
   - **Processor**: Transforms valid messages
   - **Storage**: Stores processed messages

3. **Features:**
   - Bounded channels (capacity: 10)
   - Cancellation support
   - Error handling
   - Multiple processors (3)
   - Statistics reporting

4. **Track:**
   - Total messages
   - Validated count
   - Failed validation
   - Processed count
   - Processing time

---

## Summary

You've learned:
- ✅ Basic channel operations
- ✅ Bounded vs unbounded channels
- ✅ Backpressure handling
- ✅ Multiple producers/consumers
- ✅ Processing pipelines
- ✅ Cancellation and errors
- ✅ Real-world applications

**Key Takeaways:**
- Channels are better than BlockingCollection for async
- Use bounded channels to prevent memory issues
- Pipelines enable clean data processing
- Always handle cancellation and errors

**Module Complete!** Move to [Module 03: Clean Architecture](../../03-CleanArchitecture/)
