namespace CleanArchitecture.Domain.Events;

/// <summary>
/// Something that happened, expressed in the language of the domain.
///
/// Entities record events; they never dispatch them. Dispatching means knowing
/// about a message bus, and the domain is not allowed to know about one.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public record TaskAssignedEvent(Guid TaskId, string Assignee, DateTimeOffset OccurredAt) : IDomainEvent;

public record TaskCompletedEvent(Guid TaskId, string? Assignee, DateTimeOffset OccurredAt) : IDomainEvent;
