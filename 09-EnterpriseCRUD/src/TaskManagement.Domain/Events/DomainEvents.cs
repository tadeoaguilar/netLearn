namespace TaskManagement.Domain.Events;

public abstract record DomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public record ProjectCreatedEvent(Guid ProjectId, string Name) : DomainEvent;
public record ProjectArchivedEvent(Guid ProjectId) : DomainEvent;
public record TaskCreatedEvent(Guid TaskId, Guid ProjectId, string Title) : DomainEvent;
public record TaskAssignedEvent(Guid TaskId, string Assignee) : DomainEvent;
public record TaskCompletedEvent(Guid TaskId, string? Assignee) : DomainEvent;
