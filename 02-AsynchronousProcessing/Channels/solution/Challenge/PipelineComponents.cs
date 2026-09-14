namespace Channels.Challenge;

/// <summary>Rejects roughly one message in ten, deterministically.</summary>
public class EveryTenthFailsValidator : IMessageValidator
{
    public bool TryValidate(RawMessage message, out string? error)
    {
        if (message.Id % 10 == 0)
        {
            error = "payload failed schema check";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.Payload))
        {
            error = "payload is empty";
            return false;
        }

        error = null;
        return true;
    }
}

public class UppercaseTransformer : IMessageTransformer
{
    private readonly TimeSpan _cost;

    public UppercaseTransformer(TimeSpan? cost = null)
        => _cost = cost ?? TimeSpan.FromMilliseconds(20);

    public async ValueTask<ProcessedMessage> TransformAsync(
        ValidMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(_cost, cancellationToken);
        return new ProcessedMessage(message.Id, message.Payload.ToUpperInvariant());
    }
}

/// <summary>
/// In-memory sink. Writes are serialized with a lock because a single storage
/// stage still shares this instance with whatever else holds a reference.
/// </summary>
public class InMemoryMessageStore : IMessageStore
{
    private readonly List<ProcessedMessage> _saved = new();
    private readonly Lock _gate = new();
    private readonly TimeSpan _cost;

    public InMemoryMessageStore(TimeSpan? cost = null)
        => _cost = cost ?? TimeSpan.FromMilliseconds(5);

    public IReadOnlyList<ProcessedMessage> Saved
    {
        get { lock (_gate) return _saved.ToArray(); }
    }

    public async ValueTask SaveAsync(ProcessedMessage message, CancellationToken cancellationToken)
    {
        if (_cost > TimeSpan.Zero)
        {
            await Task.Delay(_cost, cancellationToken);
        }

        lock (_gate) _saved.Add(message);
    }
}
