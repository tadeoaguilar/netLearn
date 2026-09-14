using TaskManagement.Domain.Common;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Events;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.ValueObjects;

namespace TaskManagement.Domain.Entities;

/// <summary>
/// The aggregate root. Tasks are created through it, which is what lets it
/// enforce rules spanning the whole collection.
/// </summary>
public class Project : AuditableEntity
{
    private readonly List<TaskItem> _tasks = new();

    private Project() { }   // EF Core

    private Project(Title name, string? description, UserId owner)
    {
        Name = name.Value;
        Description = description;
        OwnerId = owner.Value;
        Status = ProjectStatus.Active;
    }

    public const int MaxOpenTasks = 100;

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public ProjectStatus Status { get; private set; }

    public IReadOnlyCollection<TaskItem> Tasks => _tasks;

    public int OpenTaskCount => _tasks.Count(t =>
        t.State is TaskState.Todo or TaskState.InProgress or TaskState.InReview);

    public static Project Create(string name, string? description, string ownerId)
    {
        var project = new Project(Title.Create(name), description?.Trim(), UserId.Create(ownerId));
        project.Raise(new ProjectCreatedEvent(project.Id, project.Name));
        return project;
    }

    public void Rename(string name)
    {
        EnsureModifiable();
        Name = Title.Create(name).Value;
    }

    public void ChangeDescription(string? description)
    {
        EnsureModifiable();
        Description = description?.Trim();
    }

    public void ChangeStatus(ProjectStatus status)
    {
        if (Status == ProjectStatus.Archived)
            throw new DomainException("An archived project cannot change status.");

        if (status == ProjectStatus.Archived)
        {
            Archive();
            return;
        }

        Status = status;
    }

    public void Archive()
    {
        if (Status == ProjectStatus.Archived) return;

        if (OpenTaskCount > 0)
            throw new DomainException($"Cannot archive a project with {OpenTaskCount} open task(s).");

        Status = ProjectStatus.Archived;
        Raise(new ProjectArchivedEvent(Id));
    }

    public TaskItem AddTask(string title, string? description, TaskPriority priority, DateTimeOffset? dueDate)
    {
        EnsureModifiable();

        // An invariant across the whole collection: only the root can see
        // enough to enforce it.
        if (OpenTaskCount >= MaxOpenTasks)
            throw new DomainException($"Project '{Name}' already has {MaxOpenTasks} open tasks.");

        var task = TaskItem.Create(Id, title, description, priority, dueDate);
        _tasks.Add(task);
        return task;
    }

    private void EnsureModifiable()
    {
        if (Status is ProjectStatus.Archived)
            throw new DomainException("An archived project cannot be modified.");
    }
}
