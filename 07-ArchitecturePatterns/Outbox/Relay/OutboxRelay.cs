using Outbox.Store;

namespace Outbox.Relay;

/// <summary>
/// Polls the outbox and publishes what has not gone out yet.
///
/// Note the ordering inside <see cref="PublishPendingAsync"/>: publish FIRST,
/// then mark as published. Marking first would lose the message if publishing
/// then failed. This way a crash between the two causes a DUPLICATE, not a
/// loss -- which is why every consumer downstream must be idempotent.
///
/// At-least-once is the guarantee this pattern gives you. Exactly-once is not
/// on the menu.
/// </summary>
public class OutboxRelay
{
    private readonly TransactionalStore _store;
    private readonly Func<OutboxMessage, CancellationToken, Task> _publish;
    private readonly int _maxAttempts;

    public OutboxRelay(
        TransactionalStore store,
        Func<OutboxMessage, CancellationToken, Task> publish,
        int maxAttempts = 3)
    {
        _store = store;
        _publish = publish;
        _maxAttempts = maxAttempts;
    }

    public async Task<RelayResult> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var published = 0;
        var failed = 0;
        var abandoned = 0;

        foreach (var message in _store.UnpublishedMessages)
        {
            if (message.Attempts >= _maxAttempts)
            {
                abandoned++;
                continue;
            }

            try
            {
                await _publish(message, cancellationToken);
                _store.MarkPublished(message.Id);
                published++;
            }
            catch
            {
                _store.RecordAttempt(message.Id);
                failed++;
            }
        }

        return new RelayResult(published, failed, abandoned);
    }
}

public record RelayResult(int Published, int Failed, int Abandoned);
