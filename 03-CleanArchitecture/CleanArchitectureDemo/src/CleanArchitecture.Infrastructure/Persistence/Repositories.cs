using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Persistence;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context) => _context = context;

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Projects
            .Include(p => p.Tasks)   // the aggregate must load whole, or its rules cannot run
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Projects
            .Include(p => p.Tasks)
            .AsNoTracking()          // read-only: skip change tracking entirely
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default)
        => await _context.Projects.AddAsync(project, cancellationToken);
}

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context) => _context = context;

    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TaskItem>> SearchAsync(
        TaskFilter filter, CancellationToken cancellationToken = default)
    {
        // The filter is translated into a query HERE, in Infrastructure.
        // Application described what it wanted without naming a query language.
        var query = _context.Tasks.AsNoTracking().AsQueryable();

        if (filter.ProjectId is { } projectId)
        {
            query = query.Where(t => t.ProjectId == projectId);
        }

        if (filter.State is { } state)
        {
            query = query.Where(t => t.State == state);
        }

        if (!string.IsNullOrWhiteSpace(filter.Assignee))
        {
            query = query.Where(t => t.Assignee == filter.Assignee);
        }

        if (filter.MinimumPriority is { } minimum)
        {
            query = query.Where(t => t.Priority >= minimum);
        }

        return await query
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Wraps SaveChangesAsync and dispatches whatever domain events the entities
/// recorded, but only once the save has actually succeeded.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly IDomainEventDispatcher _dispatcher;

    public UnitOfWork(AppDbContext context, IDomainEventDispatcher dispatcher)
    {
        _context = context;
        _dispatcher = dispatcher;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entities = _context.ChangeTracker
            .Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToArray();

        var events = entities.SelectMany(e => e.DomainEvents).ToArray();

        var written = await _context.SaveChangesAsync(cancellationToken);

        // Clear before dispatching, so a handler that saves again does not
        // replay the same events.
        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        await _dispatcher.DispatchAsync(events, cancellationToken);

        return written;
    }
}
