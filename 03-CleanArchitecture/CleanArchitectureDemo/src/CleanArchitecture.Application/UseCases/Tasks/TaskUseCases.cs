using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.DTOs;
using CleanArchitecture.Domain.Enums;
using CleanArchitecture.Domain.Exceptions;

namespace CleanArchitecture.Application.UseCases.Tasks;

public class CreateTaskUseCase
{
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateTaskUseCase(IProjectRepository projects, IUnitOfWork unitOfWork, IClock clock)
    {
        _projects = projects;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<TaskDto>> ExecuteAsync(
        Guid projectId,
        string title,
        string? description,
        Priority priority,
        CancellationToken cancellationToken = default)
    {
        // Load the aggregate root, not the task. The project is what enforces
        // the "no more than 50 open tasks" rule -- creating a TaskItem directly
        // would bypass it.
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);

        if (project is null)
        {
            return Result<TaskDto>.NotFound($"Project {projectId} was not found.");
        }

        try
        {
            var task = project.AddTask(title, description, priority, _clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<TaskDto>.Success(TaskDto.From(task));
        }
        catch (DomainException ex)
        {
            return Result<TaskDto>.Invalid(ex.Message);
        }
    }
}

public class AssignTaskUseCase
{
    private readonly ITaskRepository _tasks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly INotificationService _notifications;

    public AssignTaskUseCase(
        ITaskRepository tasks,
        IUnitOfWork unitOfWork,
        IClock clock,
        INotificationService notifications)
    {
        _tasks = tasks;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<Result<TaskDto>> ExecuteAsync(
        Guid taskId,
        string assignee,
        CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(taskId, cancellationToken);

        if (task is null)
        {
            return Result<TaskDto>.NotFound($"Task {taskId} was not found.");
        }

        try
        {
            task.AssignTo(assignee, _clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<TaskDto>.Conflict(ex.Message);
        }

        // Notify only after the save succeeded. Sending first would mean
        // emailing someone about an assignment that then failed to persist.
        await _notifications.NotifyAsync(
            assignee,
            $"Task assigned: {task.Title}",
            $"You have been assigned task {task.Id}.",
            cancellationToken);

        return Result<TaskDto>.Success(TaskDto.From(task));
    }
}

public class CompleteTaskUseCase
{
    private readonly ITaskRepository _tasks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CompleteTaskUseCase(ITaskRepository tasks, IUnitOfWork unitOfWork, IClock clock)
    {
        _tasks = tasks;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<TaskDto>> ExecuteAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(taskId, cancellationToken);

        if (task is null)
        {
            return Result<TaskDto>.NotFound($"Task {taskId} was not found.");
        }

        try
        {
            task.Complete(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<TaskDto>.Success(TaskDto.From(task));
        }
        catch (DomainException ex)
        {
            return Result<TaskDto>.Conflict(ex.Message);
        }
    }
}

public class SearchTasksUseCase
{
    private readonly ITaskRepository _tasks;

    public SearchTasksUseCase(ITaskRepository tasks) => _tasks = tasks;

    public async Task<IReadOnlyList<TaskDto>> ExecuteAsync(
        TaskFilter filter,
        CancellationToken cancellationToken = default)
    {
        var tasks = await _tasks.SearchAsync(filter, cancellationToken);
        return tasks.Select(TaskDto.From).ToList();
    }
}
