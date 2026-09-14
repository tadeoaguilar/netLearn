using System.Collections.Concurrent;

namespace MessageQueues.Broker;

/// <summary>
/// At-least-once delivery means your handler WILL see the same message twice.
/// Not might -- will, eventually. A retry after a timeout that actually
/// succeeded, a broker failover, a redelivery after a crash between the work
/// and the acknowledgement.
///
/// The fix is not a better broker. It is a consumer that can safely see a
/// message twice, which means recording what it has already processed.
/// </summary>
public class IdempotentConsumer
{
    private readonly ConcurrentDictionary<string, byte> _processed = new();
    private readonly Func<Message, CancellationToken, Task> _inner;

    public IdempotentConsumer(Func<Message, CancellationToken, Task> inner) => _inner = inner;

    public int Processed => _processed.Count;
    public int DuplicatesSkipped { get; private set; }

    public async Task HandleAsync(Message message, CancellationToken cancellationToken = default)
    {
        // TryAdd is the whole trick: it returns false if the key was already
        // there, atomically. Check-then-act would race.
        if (!_processed.TryAdd(message.Id, 0))
        {
            DuplicatesSkipped++;
            return;
        }

        try
        {
            await _inner(message, cancellationToken);
        }
        catch
        {
            // Roll the record back, or a transient failure would be remembered
            // as a success and the retry silently skipped.
            _processed.TryRemove(message.Id, out _);
            throw;
        }
    }
}
