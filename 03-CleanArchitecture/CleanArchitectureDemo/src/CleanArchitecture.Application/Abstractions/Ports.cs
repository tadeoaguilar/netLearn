using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Events;

namespace CleanArchitecture.Application.Abstractions;

/// <summary>
/// Note the direction: this interface is declared HERE, in Application, and
/// implemented out in Infrastructure. Application never references the
/// implementation, so the database can be swapped without touching a use case.
/// </summary>
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Project project, CancellationToken cancellationToken = default);
}

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> SearchAsync(TaskFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>Search criteria, expressed without any reference to a query language.</summary>
public record TaskFilter(
    Guid? ProjectId = null,
    Domain.Enums.TaskState? State = null,
    string? Assignee = null,
    Domain.Enums.Priority? MinimumPriority = null);

/// <summary>
/// Commits a unit of work. Application decides WHEN to save; Infrastructure
/// decides HOW.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Time as a dependency. Use cases never call DateTimeOffset.UtcNow directly,
/// which is what lets a test pin "now" to a known instant.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>An outbound port to the world. Infrastructure decides if that is
/// SMTP, a queue, or a console line.</summary>
public interface INotificationService
{
    Task NotifyAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);
}

/// <summary>Publishes the events entities recorded, after the save succeeds.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
}
