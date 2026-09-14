using TaskManagement.Domain.Common;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Events;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.ValueObjects;

namespace TaskManagement.Domain.Entities;

public class TaskItem : AuditableEntity
{
    private TaskItem() { }   // EF Core

    private TaskItem(Guid projectId, Title title, string? description, TaskPriority priority, DateTimeOffset? dueDate)
    {
        ProjectId = projectId;
        Title = title.Value;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
        State = TaskState.Todo;
    }

    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskPriority Priority { get; private set; }
    public TaskState State { get; private set; }
    public string? AssigneeId { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsOverdue(DateTimeOffset now) =>
        DueDate is { } due && State is not (TaskState.Done or TaskState.Cancelled) && due < now;

    internal static TaskItem Create(
        Guid projectId, string title, string? description, TaskPriority priority, DateTimeOffset? dueDate)
    {
        if (projectId == Guid.Empty)
            throw new DomainException("A task must belong to a project.");

        // Qualified because the Title PROPERTY on this class shadows the
        // Title TYPE. A good argument for not naming a property after its type.
        return new TaskItem(
            projectId, ValueObjects.Title.Create(title), description?.Trim(), priority, dueDate);
    }

    public void UpdateDetails(string title, string? description, TaskPriority priority, DateTimeOffset? dueDate)
    {
        EnsureOpen();

        Title = ValueObjects.Title.Create(title).Value;
        Description = description?.Trim();
        Priority = priority;
        DueDate = dueDate;
    }

    public void AssignTo(string assigneeId)
    {
        EnsureOpen();

        AssigneeId = UserId.Create(assigneeId).Value;

        if (State == TaskState.Todo)
        {
            State = TaskState.InProgress;
        }

        Raise(new TaskAssignedEvent(Id, AssigneeId));
    }

    public void Unassign()
    {
        EnsureOpen();
        AssigneeId = null;
    }

    public void MoveTo(TaskState state, DateTimeOffset now)
    {
        if (State == state) return;

        // An explicit transition table beats scattered if-statements: the legal
        // moves are stated in one place and can be read at a glance.
        var allowed = State switch
        {
            TaskState.Todo => new[] { TaskState.InProgress, TaskState.Cancelled },
            TaskState.InProgress => new[] { TaskState.InReview, TaskState.Todo, TaskState.Cancelled },
            TaskState.InReview => new[] { TaskState.Done, TaskState.InProgress, TaskState.Cancelled },
            TaskState.Done => Array.Empty<TaskState>(),
            TaskState.Cancelled => Array.Empty<TaskState>(),
            _ => Array.Empty<TaskState>()
        };

        if (!allowed.Contains(state))
            throw new DomainException($"Cannot move a task from {State} to {state}.");

        if (state == TaskState.Done)
        {
            Complete(now);
            return;
        }

        State = state;
    }

    public void Complete(DateTimeOffset now)
    {
        if (State == TaskState.Done)
            throw new DomainException("Task is already complete.");

        if (State == TaskState.Cancelled)
            throw new DomainException("Cannot complete a cancelled task.");

        if (AssigneeId is null)
            throw new DomainException("A task must be assigned before it can be completed.");

        State = TaskState.Done;
        CompletedAt = now;
        Raise(new TaskCompletedEvent(Id, AssigneeId));
    }

    public void Cancel()
    {
        if (State == TaskState.Done)
            throw new DomainException("Cannot cancel a completed task.");

        State = TaskState.Cancelled;
    }

    private void EnsureOpen()
    {
        if (State is TaskState.Done or TaskState.Cancelled)
            throw new DomainException($"Cannot modify a task that is {State}.");
    }
}
