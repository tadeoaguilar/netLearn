using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Common.Database;
using VerticalSlice.Api.Common.Domain;

namespace VerticalSlice.Api.Features.Tasks.AssignTask;

public record AssignTaskCommand(Guid TaskId, string Assignee) : IRequest<Result<TaskDto>>;

public class AssignTaskValidator : AbstractValidator<AssignTaskCommand>
{
    public AssignTaskValidator()
    {
        RuleFor(x => x.Assignee).NotEmpty().WithMessage("Assignee is required.").MaximumLength(200);
    }
}

/// <summary>Notification port, declared by the one slice that needs it.</summary>
public interface ITaskNotifier
{
    Task NotifyAssignedAsync(string assignee, string taskTitle, CancellationToken cancellationToken);
}

public class ConsoleTaskNotifier : ITaskNotifier
{
    public Task NotifyAssignedAsync(string assignee, string taskTitle, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[notify] to={assignee} task={taskTitle}");
        return Task.CompletedTask;
    }
}

public class AssignTaskHandler : IRequestHandler<AssignTaskCommand, Result<TaskDto>>
{
    private readonly AppDbContext _context;
    private readonly ITaskNotifier _notifier;

    public AssignTaskHandler(AppDbContext context, ITaskNotifier notifier)
    {
        _context = context;
        _notifier = notifier;
    }

    public async Task<Result<TaskDto>> Handle(AssignTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

        if (task is null)
        {
            return Result<TaskDto>.NotFound($"Task {request.TaskId} was not found.");
        }

        if (task.State is TaskState.Done or TaskState.Cancelled)
        {
            return Result<TaskDto>.Conflict($"Cannot assign a task that is {task.State}.");
        }

        task.Assignee = request.Assignee.Trim();

        if (task.State == TaskState.Todo)
        {
            task.State = TaskState.InProgress;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // After the save, for the same reason as module 03.
        await _notifier.NotifyAssignedAsync(task.Assignee, task.Title, cancellationToken);

        return Result<TaskDto>.Success(task.ToDto());
    }
}

public record AssignTaskRequest(string Assignee);

public class AssignTaskEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/tasks/{id:guid}/assign", async (
            Guid id, AssignTaskRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new AssignTaskCommand(id, request.Assignee), cancellationToken);
            return result.ToHttpResult();
        });
}
