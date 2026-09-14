namespace VerticalSlice.Api.Common.Domain;

public enum TaskState { Todo, InProgress, Done, Cancelled }

public enum Priority { Low, Normal, High, Urgent }

/// <summary>
/// Compare with module 03's TaskItem, which hides every setter behind a method.
///
/// Vertical slice usually keeps entities simpler and puts the rules in the
/// handler that needs them. That is faster to write and read for one feature --
/// and it means a rule enforced in CompleteTask is NOT enforced anywhere else
/// unless you remember to repeat it. The trade is real in both directions.
/// </summary>
public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<TaskItem> Tasks { get; set; } = new();
}

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Priority Priority { get; set; }
    public TaskState State { get; set; }
    public string? Assignee { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
