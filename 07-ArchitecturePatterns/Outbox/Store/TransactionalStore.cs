namespace Outbox.Store;

public record Order(string Id, string CustomerId, decimal Amount);

public record OutboxMessage(
    Guid Id,
    string Type,
    string Payload,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt = null,
    int Attempts = 0);

/// <summary>
/// A database that can fail mid-transaction, so the dual-write problem is
/// visible rather than theoretical.
///
/// THE PROBLEM: saving an order and publishing "OrderPlaced" are two separate
/// systems. There is no transaction spanning both. Whichever you do second can
/// fail after the first succeeded:
///
///   save, then publish  -> crash between them: order exists, nobody told.
///   publish, then save  -> crash between them: everyone told about an order
///                          that does not exist.
///
/// THE FIX: write the message into the SAME database transaction as the data,
/// into an outbox table. Now there is only one write, so it either all happens
/// or none of it does. A separate relay publishes from the outbox afterwards.
/// </summary>
public class TransactionalStore
{
    private readonly List<Order> _orders = new();
    private readonly List<OutboxMessage> _outbox = new();

    public IReadOnlyList<Order> Orders => _orders.ToArray();
    public IReadOnlyList<OutboxMessage> Outbox => _outbox.ToArray();
    public IReadOnlyList<OutboxMessage> UnpublishedMessages => _outbox.Where(m => m.PublishedAt is null).ToArray();

    /// <summary>
    /// WRONG: two separate writes. If <paramref name="crashAfterSave"/> is set,
    /// the order is stored and the message never is -- exactly what a process
    /// dying between the two lines looks like.
    /// </summary>
    public void SaveOrderThenPublish(Order order, Action publish, bool crashAfterSave = false)
    {
        _orders.Add(order);

        if (crashAfterSave)
        {
            throw new InvalidOperationException("process died after saving, before publishing");
        }

        publish();
    }

    /// <summary>
    /// RIGHT: one atomic write covering both the order and its message.
    ///
    /// In a real system these are two INSERTs inside one SQL transaction.
    /// Here, the list mutations happen together or not at all.
    /// </summary>
    public void SaveOrderWithOutbox(Order order, string eventType, string payload, bool crashDuringCommit = false)
    {
        if (crashDuringCommit)
        {
            // Nothing was written. The transaction rolled back, so there is no
            // half-state to reconcile -- which is the entire point.
            throw new InvalidOperationException("transaction rolled back");
        }

        _orders.Add(order);
        _outbox.Add(new OutboxMessage(Guid.NewGuid(), eventType, payload, DateTimeOffset.UtcNow));
    }

    public void MarkPublished(Guid messageId)
    {
        var index = _outbox.FindIndex(m => m.Id == messageId);
        if (index >= 0)
        {
            _outbox[index] = _outbox[index] with { PublishedAt = DateTimeOffset.UtcNow };
        }
    }

    public void RecordAttempt(Guid messageId)
    {
        var index = _outbox.FindIndex(m => m.Id == messageId);
        if (index >= 0)
        {
            _outbox[index] = _outbox[index] with { Attempts = _outbox[index].Attempts + 1 };
        }
    }
}
