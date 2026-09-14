using CleanArchitecture.Domain.Exceptions;

namespace CleanArchitecture.Domain.Entities;

/// <summary>
/// The aggregate root for tasks. Tasks are created through the project, which
/// is what gives the project a chance to enforce rules across all of them.
/// </summary>
public class Project : Entity
{
    private readonly List<TaskItem> _tasks = new();

    private Project() { }

    private Project(string name, DateTimeOffset createdAt)
    {
        Name = name;
        CreatedAt = createdAt;
    }

    public const int MaxOpenTasks = 50;

    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsArchived { get; private set; }

    public IReadOnlyList<TaskItem> Tasks => _tasks;

    public static Project Create(string name, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Project name is required.");
        }

        return new Project(name.Trim(), createdAt);
    }

    public TaskItem AddTask(
        string title,
        string? description,
        Enums.Priority priority,
        DateTimeOffset createdAt)
    {
        if (IsArchived)
        {
            throw new DomainException("Cannot add tasks to an archived project.");
        }

        // A rule that spans the whole collection -- exactly the kind of thing
        // an aggregate root exists to enforce. No individual TaskItem could.
        var openTasks = _tasks.Count(t => t.State is Enums.TaskState.Todo or Enums.TaskState.InProgress);

        if (openTasks >= MaxOpenTasks)
        {
            throw new DomainException(
                $"Project '{Name}' already has {MaxOpenTasks} open tasks.");
        }

        var task = TaskItem.Create(Id, title, description, priority, createdAt);
        _tasks.Add(task);
        return task;
    }

    public void Archive()
    {
        if (_tasks.Any(t => t.State is Enums.TaskState.Todo or Enums.TaskState.InProgress))
        {
            throw new DomainException("Cannot archive a project with open tasks.");
        }

        IsArchived = true;
    }
}
