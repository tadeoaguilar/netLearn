namespace TaskManagement.Domain.Enums;

/// <summary>
/// Named TaskState, not TaskStatus: System.Threading.Tasks.TaskStatus is
/// imported by ImplicitUsings and the collision is genuinely confusing.
/// </summary>
public enum TaskState
{
    Todo = 0,
    InProgress = 1,
    InReview = 2,
    Done = 3,
    Cancelled = 4
}

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum ProjectStatus
{
    Active = 0,
    OnHold = 1,
    Completed = 2,
    Archived = 3
}
