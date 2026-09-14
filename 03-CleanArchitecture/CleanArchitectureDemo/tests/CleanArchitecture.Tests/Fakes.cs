using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Events;

namespace CleanArchitecture.Tests;

/// <summary>
/// Hand-written fakes rather than a mocking framework. For ports this small,
/// a real in-memory implementation reads better than a pile of setup calls --
/// and it behaves consistently across a whole test.
/// </summary>
public class FakeProjectRepository : IProjectRepository
{
    private readonly Dictionary<Guid, Project> _projects = new();

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_projects.GetValueOrDefault(id));

    public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Project>>(_projects.Values.ToList());

    public Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        _projects[project.Id] = project;
        return Task.CompletedTask;
    }

    public void Seed(Project project) => _projects[project.Id] = project;
}

public class FakeTaskRepository : ITaskRepository
{
    private readonly List<TaskItem> _tasks = new();

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_tasks.FirstOrDefault(t => t.Id == id));

    public Task<IReadOnlyList<TaskItem>> SearchAsync(
        TaskFilter filter, CancellationToken cancellationToken = default)
    {
        IEnumerable<TaskItem> query = _tasks;

        if (filter.ProjectId is { } projectId) query = query.Where(t => t.ProjectId == projectId);
        if (filter.State is { } state) query = query.Where(t => t.State == state);
        if (!string.IsNullOrWhiteSpace(filter.Assignee)) query = query.Where(t => t.Assignee == filter.Assignee);
        if (filter.MinimumPriority is { } min) query = query.Where(t => t.Priority >= min);

        return Task.FromResult<IReadOnlyList<TaskItem>>(
            query.OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt).ToList());
    }

    public void Seed(params TaskItem[] tasks) => _tasks.AddRange(tasks);
}

public class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

/// <summary>A clock frozen at a known instant, so assertions can be exact.</summary>
public class FixedClock : IClock
{
    public FixedClock(DateTimeOffset now) => UtcNow = now;
    public DateTimeOffset UtcNow { get; set; }
}

public class RecordingNotificationService : INotificationService
{
    public List<(string Recipient, string Subject)> Sent { get; } = new();

    public Task NotifyAsync(
        string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        Sent.Add((recipient, subject));
        return Task.CompletedTask;
    }
}

public class RecordingDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> Dispatched { get; } = new();

    public Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        Dispatched.AddRange(events);
        return Task.CompletedTask;
    }
}
