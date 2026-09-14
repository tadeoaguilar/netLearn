using System.Collections.Concurrent;

namespace EventDriven.Bus;

/// <summary>An event: a fact about something that already happened.</summary>
public interface IEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

public abstract record EventBase : IEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Publish/subscribe: the publisher does not know who is listening, or whether
/// anyone is. That is the difference from a command, which names its handler.
///
/// A queue delivers each message to ONE consumer (module MessageQueues); an
/// event bus delivers each event to EVERY subscriber. Choosing the wrong one is
/// the most common mistake in this space.
/// </summary>
public class EventBus
{
    private readonly ConcurrentDictionary<Type, List<Func<IEvent, CancellationToken, Task>>> _handlers = new();
    private readonly List<string> _log = new();

    public IReadOnlyList<string> Log => _log.ToArray();

    public void Subscribe<TEvent>(string subscriberName, Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IEvent
    {
        var wrapped = (IEvent e, CancellationToken token) => handler((TEvent)e, token);

        _handlers.AddOrUpdate(
            typeof(TEvent),
            _ => [wrapped],
            (_, existing) => { existing.Add(wrapped); return existing; });

        _log.Add($"subscribe {subscriberName} -> {typeof(TEvent).Name}");
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        _log.Add($"publish {typeof(TEvent).Name}");

        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            // Nobody listening is not an error. A publisher with no subscribers
            // must behave exactly as it does with ten.
            return;
        }

        // One failing subscriber must not stop the others. In a real bus each
        // subscriber has its own queue and its own retry budget for this reason.
        foreach (var handler in handlers.ToArray())
        {
            try
            {
                await handler(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _log.Add($"handler failed: {ex.Message}");
            }
        }
    }
}
