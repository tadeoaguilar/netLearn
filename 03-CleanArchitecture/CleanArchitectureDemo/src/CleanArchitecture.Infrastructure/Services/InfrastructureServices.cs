using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Domain.Events;

namespace CleanArchitecture.Infrastructure.Services;

public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Writes notifications to the console. Swapping this for SMTP, SendGrid or a
/// queue requires no change anywhere else -- Application only knows the port.
/// </summary>
public class ConsoleNotificationService : INotificationService
{
    private readonly List<string> _sent = new();

    public IReadOnlyList<string> Sent => _sent;

    public Task NotifyAsync(
        string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        _sent.Add($"{recipient}|{subject}");
        Console.WriteLine($"[notify] to={recipient} subject={subject}");
        return Task.CompletedTask;
    }
}

public class LoggingDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
        {
            Console.WriteLine($"[event] {domainEvent.GetType().Name} at {domainEvent.OccurredAt:O}");
        }

        return Task.CompletedTask;
    }
}
