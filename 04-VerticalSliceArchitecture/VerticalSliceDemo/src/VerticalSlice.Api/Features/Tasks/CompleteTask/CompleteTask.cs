using MediatR;
using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Common.Database;
using VerticalSlice.Api.Common.Domain;

namespace VerticalSlice.Api.Features.Tasks.CompleteTask;

public record CompleteTaskCommand(Guid TaskId) : IRequest<Result<TaskDto>>;

public class CompleteTaskHandler : IRequestHandler<CompleteTaskCommand, Result<TaskDto>>
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _time;

    public CompleteTaskHandler(AppDbContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    public async Task<Result<TaskDto>> Handle(CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

        if (task is null)
        {
            return Result<TaskDto>.NotFound($"Task {request.TaskId} was not found.");
        }

        if (task.State == TaskState.Done)
        {
            return Result<TaskDto>.Conflict("Task is already complete.");
        }

        if (task.State == TaskState.Cancelled)
        {
            return Result<TaskDto>.Conflict("Cannot complete a cancelled task.");
        }

        // The same rule as module 03's TaskItem.Complete(), restated here
        // because in this layout there is no entity to hold it.
        if (task.Assignee is null)
        {
            return Result<TaskDto>.Conflict("A task must be assigned before it can be completed.");
        }

        task.State = TaskState.Done;
        task.CompletedAt = _time.GetUtcNow();

        await _context.SaveChangesAsync(cancellationToken);

        return Result<TaskDto>.Success(task.ToDto());
    }
}

public class CompleteTaskEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/tasks/{id:guid}/complete", async (
            Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new CompleteTaskCommand(id), cancellationToken);
            return result.ToHttpResult();
        });
}
