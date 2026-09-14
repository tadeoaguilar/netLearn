namespace EventSourcing.Store;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public record StoredEvent(string StreamId, long Version, IDomainEvent Event);

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}

/// <summary>
/// An append-only log of facts, keyed by stream.
///
/// The crucial inversion: in a normal system the current state is the truth and
/// history is an audit log you hope someone wrote. Here the HISTORY is the
/// truth and current state is derived. You can never lose information you did
/// not think to record, because you record what happened rather than its
/// consequence.
/// </summary>
public class InMemoryEventStore
{
    private readonly Dictionary<string, List<StoredEvent>> _streams = new();

    public IReadOnlyList<StoredEvent> Read(string streamId, long fromVersion = 0)
        => _streams.TryGetValue(streamId, out var stream)
            ? stream.Where(e => e.Version > fromVersion).ToArray()
            : [];

    public long GetVersion(string streamId)
        => _streams.TryGetValue(streamId, out var stream) && stream.Count > 0
            ? stream[^1].Version
            : 0;

    /// <summary>
    /// Appends events, refusing if the stream moved since the caller read it.
    ///
    /// This is OPTIMISTIC CONCURRENCY, and it is why event sourcing handles
    /// concurrent writers well: two users acting on version 4 cannot both
    /// succeed, because the second one's expected version no longer matches.
    /// </summary>
    public void Append(string streamId, long expectedVersion, IReadOnlyList<IDomainEvent> events)
    {
        var stream = _streams.TryGetValue(streamId, out var existing) ? existing : _streams[streamId] = [];
        var currentVersion = stream.Count > 0 ? stream[^1].Version : 0;

        if (currentVersion != expectedVersion)
        {
            throw new ConcurrencyException(
                $"Stream '{streamId}' is at version {currentVersion}, caller expected {expectedVersion}.");
        }

        foreach (var @event in events)
        {
            stream.Add(new StoredEvent(streamId, ++currentVersion, @event));
        }
    }

    public IReadOnlyList<StoredEvent> ReadAll()
        => _streams.Values.SelectMany(s => s).OrderBy(e => e.Event.OccurredAt).ToArray();

    public int TotalEvents => _streams.Values.Sum(s => s.Count);
}
