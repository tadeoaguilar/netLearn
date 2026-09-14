namespace CleanArchitecture.Domain.Enums;

/// <summary>
/// Named TaskState rather than TaskStatus to avoid colliding with
/// System.Threading.Tasks.TaskStatus, which is imported by ImplicitUsings.
/// </summary>
public enum TaskState
{
    Todo,
    InProgress,
    Done,
    Cancelled
}

public enum Priority
{
    Low,
    Normal,
    High,
    Urgent
}
