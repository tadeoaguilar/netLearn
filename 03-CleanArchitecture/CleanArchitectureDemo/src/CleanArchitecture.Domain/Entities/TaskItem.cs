using CleanArchitecture.Domain.Enums;
using CleanArchitecture.Domain.Events;
using CleanArchitecture.Domain.Exceptions;
using CleanArchitecture.Domain.ValueObjects;

namespace CleanArchitecture.Domain.Entities;

/// <summary>
/// A unit of work on a project.
///
/// Every property has a private setter and every change goes through a method
/// that enforces a rule. That is what makes this a domain entity rather than a
/// bag of properties: you cannot put it into an invalid state from outside.
/// </summary>
public class TaskItem : Entity
{
    // EF Core needs a parameterless constructor. It is private, so application
    // code cannot use it to sidestep the rules below.
    private TaskItem() { }

    private TaskItem(Guid projectId, TaskTitle title, string? description, Priority priority, DateTimeOffset createdAt)
    {
        ProjectId = projectId;
        Title = title.Value;
        Description = description;
        Priority = priority;
        State = TaskState.Todo;
        CreatedAt = createdAt;
    }

    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Priority Priority { get; private set; }
    public TaskState State { get; private set; }
    public string? Assignee { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static TaskItem Create(
        Guid projectId,
        string title,
        string? description,
        Priority priority,
        DateTimeOffset createdAt)
    {
        if (projectId == Guid.Empty)
        {
            throw new DomainException("A task must belong to a project.");
        }

        // TaskTitle.Create does the validating -- this method does not repeat it.
        return new TaskItem(projectId, TaskTitle.Create(title), description, priority, createdAt);
    }

    public void AssignTo(string assignee, DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(assignee))
        {
            throw new DomainException("Assignee is required.");
        }

        if (State is TaskState.Done or TaskState.Cancelled)
        {
            throw new DomainException($"Cannot assign a task that is {State}.");
        }

        Assignee = assignee.Trim();

        if (State == TaskState.Todo)
        {
            State = TaskState.InProgress;
        }

        Raise(new TaskAssignedEvent(Id, Assignee, occurredAt));
    }

    public void Complete(DateTimeOffset completedAt)
    {
        if (State == TaskState.Done)
        {
            throw new DomainException("Task is already complete.");
        }

        if (State == TaskState.Cancelled)
        {
            throw new DomainException("Cannot complete a cancelled task.");
        }

        if (Assignee is null)
        {
            throw new DomainException("A task must be assigned before it can be completed.");
        }

        State = TaskState.Done;
        CompletedAt = completedAt;

        Raise(new TaskCompletedEvent(Id, Assignee, completedAt));
    }

    public void Cancel()
    {
        if (State == TaskState.Done)
        {
            throw new DomainException("Cannot cancel a completed task.");
        }

        State = TaskState.Cancelled;
    }

    public void ChangePriority(Priority priority)
    {
        if (State is TaskState.Done or TaskState.Cancelled)
        {
            throw new DomainException($"Cannot reprioritise a task that is {State}.");
        }

        Priority = priority;
    }
}
