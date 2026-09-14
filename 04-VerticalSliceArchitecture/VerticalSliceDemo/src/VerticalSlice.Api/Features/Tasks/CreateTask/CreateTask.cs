using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Common.Database;
using VerticalSlice.Api.Common.Domain;

namespace VerticalSlice.Api.Features.Tasks.CreateTask;

public record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    string? Description,
    Priority Priority = Priority.Normal) : IRequest<Result<TaskDto>>;

public class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Task title is required.")
            .MaximumLength(200);

        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ProjectId).NotEmpty();
    }
}

public class CreateTaskHandler : IRequestHandler<CreateTaskCommand, Result<TaskDto>>
{
    public const int MaxOpenTasks = 50;

    private readonly AppDbContext _context;
    private readonly TimeProvider _time;

    public CreateTaskHandler(AppDbContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    public async Task<Result<TaskDto>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project is null)
        {
            return Result<TaskDto>.NotFound($"Project {request.ProjectId} was not found.");
        }

        if (project.IsArchived)
        {
            return Result<TaskDto>.Conflict("Cannot add tasks to an archived project.");
        }

        // Counting in the database rather than loading the whole collection.
        // Module 03 has to load the full aggregate to enforce the same rule --
        // correct, but heavier. A slice can make this call for itself.
        var openTasks = await _context.Tasks.CountAsync(
            t => t.ProjectId == project.Id && (t.State == TaskState.Todo || t.State == TaskState.InProgress),
            cancellationToken);

        if (openTasks >= MaxOpenTasks)
        {
            return Result<TaskDto>.Conflict($"Project '{project.Name}' already has {MaxOpenTasks} open tasks.");
        }

        var task = new TaskItem
        {
            ProjectId = project.Id,
            Title = request.Title.Trim(),
            Description = request.Description,
            Priority = request.Priority,
            State = TaskState.Todo,
            CreatedAt = _time.GetUtcNow()
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<TaskDto>.Success(task.ToDto());
    }
}

public record CreateTaskRequest(string Title, string? Description, Priority Priority = Priority.Normal);

public class CreateTaskEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/projects/{id:guid}/tasks", async (
            Guid id, CreateTaskRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateTaskCommand(id, request.Title, request.Description, request.Priority);
            var result = await sender.Send(command, cancellationToken);
            return result.ToHttpResult(dto => Results.Created($"/tasks/{dto.Id}", dto));
        });
}
