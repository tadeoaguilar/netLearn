using System.Collections.Concurrent;

namespace MessageQueues.Broker;

/// <summary>
/// An in-process message broker with the semantics that actually matter:
/// acknowledgement, redelivery, a retry limit and a dead-letter queue.
///
/// Why not RabbitMQ? Because every behaviour this module teaches -- at-least-
/// once delivery, duplicate handling, poison messages -- is a property of the
/// PROTOCOL, not of any particular server. Modelling it in-process means the
/// tests run offline in milliseconds and you can see the whole mechanism.
/// Swapping this for a real broker is Part 6 of the exercise.
/// </summary>
public record Message(string Id, string Topic, string Body, int DeliveryCount = 1)
{
    public Message WithRedelivery() => this with { DeliveryCount = DeliveryCount + 1 };
}

public record BrokerOptions
{
    /// <summary>Attempts before a message is dead-lettered.</summary>
    public int MaxDeliveries { get; init; } = 3;
}

public class InMemoryBroker
{
    private readonly ConcurrentQueue<Message> _queue = new();
    private readonly ConcurrentQueue<(Message Message, string Reason)> _deadLetters = new();
    private readonly BrokerOptions _options;

    public InMemoryBroker(BrokerOptions? options = null) => _options = options ?? new BrokerOptions();

    public int QueueDepth => _queue.Count;
    public IReadOnlyList<(Message Message, string Reason)> DeadLetters => _deadLetters.ToArray();

    public void Publish(string topic, string body, string? id = null)
        => _queue.Enqueue(new Message(id ?? Guid.NewGuid().ToString("N"), topic, body));

    /// <summary>
    /// Delivers each queued message to the handler.
    ///
    /// A handler that throws does NOT lose the message: it goes back on the
    /// queue with an incremented delivery count, and is dead-lettered once the
    /// limit is reached. That combination -- redeliver on failure, give up
    /// eventually -- is what "at-least-once with a poison queue" means.
    /// </summary>
    public async Task<DrainResult> DrainAsync(
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var delivered = 0;
        var failed = 0;

        // Snapshot the depth so a handler that republishes cannot loop forever.
        var budget = _queue.Count * _options.MaxDeliveries + _queue.Count;

        while (_queue.TryDequeue(out var message) && budget-- > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await handler(message, cancellationToken);
                delivered++;
            }
            catch (Exception ex)
            {
                failed++;

                if (message.DeliveryCount >= _options.MaxDeliveries)
                {
                    _deadLetters.Enqueue((message, ex.Message));
                }
                else
                {
                    _queue.Enqueue(message.WithRedelivery());
                }
            }
        }

        return new DrainResult(delivered, failed, _deadLetters.Count);
    }
}

public record DrainResult(int Delivered, int Failed, int DeadLettered);
