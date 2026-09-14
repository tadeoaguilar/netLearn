using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace Channels.Challenge;

public record RawMessage(int Id, string Payload);
public record ValidMessage(int Id, string Payload);
public record ProcessedMessage(int Id, string Result);

public record PipelineStatistics(
    int Produced,
    int Validated,
    int FailedValidation,
    int Processed,
    int Stored,
    TimeSpan Elapsed,
    IReadOnlyList<string> ValidationErrors)
{
    public double ValidationRate => Produced == 0 ? 0 : (double)Validated / Produced;
}

public interface IMessageValidator
{
    bool TryValidate(RawMessage message, out string? error);
}

public interface IMessageTransformer
{
    ValueTask<ProcessedMessage> TransformAsync(ValidMessage message, CancellationToken cancellationToken);
}

public interface IMessageStore
{
    ValueTask SaveAsync(ProcessedMessage message, CancellationToken cancellationToken);
    IReadOnlyList<ProcessedMessage> Saved { get; }
}

public record PipelineOptions
{
    public int Capacity { get; init; } = 10;
    public int ProcessorCount { get; init; } = 3;
}

/// <summary>
/// Four stages joined by three bounded channels:
///
///     producer -> [raw] -> validator -> [valid] -> processor x3 -> [done] -> storage
///
/// Every stage runs concurrently. Because the channels are bounded, a slow
/// storage stage eventually slows the producer down rather than accumulating
/// an unbounded backlog in memory.
/// </summary>
public class MessageProcessingSystem
{
    private readonly IMessageValidator _validator;
    private readonly IMessageTransformer _transformer;
    private readonly IMessageStore _store;
    private readonly PipelineOptions _options;

    public MessageProcessingSystem(
        IMessageValidator validator,
        IMessageTransformer transformer,
        IMessageStore store,
        PipelineOptions? options = null)
    {
        _validator = validator;
        _transformer = transformer;
        _store = store;
        _options = options ?? new PipelineOptions();
    }

    public async Task<PipelineStatistics> RunAsync(
        IReadOnlyList<RawMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var raw = Channel.CreateBounded<RawMessage>(_options.Capacity);
        var valid = Channel.CreateBounded<ValidMessage>(_options.Capacity);
        var done = Channel.CreateBounded<ProcessedMessage>(_options.Capacity);

        var produced = 0;
        var validated = 0;
        var failed = 0;
        var processed = 0;
        var stored = 0;
        var errors = new ConcurrentBag<string>();

        var sw = Stopwatch.StartNew();

        var producer = Task.Run(async () =>
        {
            try
            {
                foreach (var message in messages)
                {
                    await raw.Writer.WriteAsync(message, cancellationToken);
                    Interlocked.Increment(ref produced);
                }

                raw.Writer.Complete();
            }
            catch (Exception ex)
            {
                // Faulting the channel rather than completing it means the
                // validator learns something went wrong instead of assuming
                // the stream simply ended.
                raw.Writer.Complete(ex);
            }
        }, cancellationToken);

        var validator = Task.Run(async () =>
        {
            try
            {
                await foreach (var message in raw.Reader.ReadAllAsync(cancellationToken))
                {
                    if (_validator.TryValidate(message, out var error))
                    {
                        Interlocked.Increment(ref validated);
                        await valid.Writer.WriteAsync(
                            new ValidMessage(message.Id, message.Payload), cancellationToken);
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                        errors.Add($"#{message.Id}: {error}");
                    }
                }

                valid.Writer.Complete();
            }
            catch (Exception ex)
            {
                valid.Writer.Complete(ex);
            }
        }, cancellationToken);

        // Several processors share one reader, so work is load balanced between
        // them. Whichever is free takes the next valid message.
        var processors = Enumerable.Range(0, _options.ProcessorCount)
            .Select(_ => Task.Run(async () =>
            {
                await foreach (var message in valid.Reader.ReadAllAsync(cancellationToken))
                {
                    var result = await _transformer.TransformAsync(message, cancellationToken);
                    Interlocked.Increment(ref processed);
                    await done.Writer.WriteAsync(result, cancellationToken);
                }
            }, cancellationToken))
            .ToArray();

        // Only the LAST processor to finish may complete the output channel,
        // so this waits for all of them first.
        var processorCompletion = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(processors);
                done.Writer.Complete();
            }
            catch (Exception ex)
            {
                done.Writer.Complete(ex);
            }
        }, CancellationToken.None);

        var storage = Task.Run(async () =>
        {
            await foreach (var message in done.Reader.ReadAllAsync(cancellationToken))
            {
                await _store.SaveAsync(message, cancellationToken);
                Interlocked.Increment(ref stored);
            }
        }, cancellationToken);

        await Task.WhenAll(producer, validator, processorCompletion, storage);
        sw.Stop();

        return new PipelineStatistics(
            produced, validated, failed, processed, stored, sw.Elapsed, errors.ToArray());
    }
}
