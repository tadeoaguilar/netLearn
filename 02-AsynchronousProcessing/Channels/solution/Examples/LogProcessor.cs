using System.Threading.Channels;

namespace Channels.Examples;

/// <summary>
/// Part 6: a background log processor. The pattern behind most real channel
/// use -- a hot path that must never block, and a worker that does the slow part.
/// </summary>
public class LogProcessor
{
    public record LogEntry(DateTime Timestamp, string Level, string Message);

    private readonly Channel<LogEntry> _logChannel;
    private readonly List<LogEntry> _storedLogs = new();

    public LogProcessor(int bufferSize = 100)
    {
        _logChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(bufferSize)
        {
            // Logging must never block the code doing the real work. If the
            // buffer is full we would rather lose old lines than stall a
            // request, so: DropOldest, and TryWrite rather than WriteAsync.
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public IReadOnlyList<LogEntry> StoredLogs => _storedLogs;

    public bool Log(string level, string message)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, message);
        return _logChannel.Writer.TryWrite(entry);
    }

    public async Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[Log Processor] Started");

        try
        {
            await foreach (var log in _logChannel.Reader.ReadAllAsync(cancellationToken))
            {
                await Task.Delay(10, cancellationToken); // write to disk, ship to a service...
                _storedLogs.Add(log);

                if (log.Level == "ERROR")
                {
                    Console.WriteLine($"[Log Processor] ERROR logged: {log.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Log Processor] Shutting down early -- buffered logs are lost.");
        }

        Console.WriteLine($"[Log Processor] Processed {_storedLogs.Count} logs total");
    }

    /// <summary>Stop accepting new logs; the processor drains what is left.</summary>
    public void Stop() => _logChannel.Writer.Complete();

    public async Task DemoLogProcessing()
    {
        Console.WriteLine("=== BACKGROUND LOG PROCESSOR ===\n");

        var processorTask = StartProcessingAsync();

        for (int i = 1; i <= 20; i++)
        {
            var level = i % 5 == 0 ? "ERROR" : "INFO";
            Log(level, $"Application event {i}");
            await Task.Delay(20);
        }

        // Complete, then await: the processor drains the buffer before exiting.
        // Cancelling instead would throw away whatever was still queued.
        Stop();
        await processorTask;

        Console.WriteLine($"\nStored {_storedLogs.Count} of 20 entries.");
        Console.WriteLine($"Errors: {_storedLogs.Count(l => l.Level == "ERROR")}");
    }
}
