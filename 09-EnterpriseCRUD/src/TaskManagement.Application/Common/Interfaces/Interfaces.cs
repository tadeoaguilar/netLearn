using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Common.Interfaces;

/// <summary>
/// The persistence port. Declared here, implemented in Infrastructure -- so
/// Application never references EF Core and the handlers stay testable.
/// </summary>
public interface IApplicationDbContext
{
    IQueryable<Project> Projects { get; }
    IQueryable<TaskItem> Tasks { get; }

    Task<Project?> FindProjectAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskItem?> FindTaskAsync(Guid id, CancellationToken cancellationToken = default);

    void AddProject(Project project);
    void RemoveProject(Project project);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Who is calling, from the validated access token.</summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}

/// <summary>Time as a dependency, so tests can pin "now".</summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
