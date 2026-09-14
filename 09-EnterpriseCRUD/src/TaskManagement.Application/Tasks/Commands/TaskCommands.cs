using FluentValidation;
using MediatR;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Projects.Commands;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;

namespace TaskManagement.Application.Tasks.Commands;

// ---------- Create ----------

public record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    string? Description,
    TaskPriority Priority = TaskPriority.Normal,
    DateTimeOffset? DueDate = null) : IRequest<TaskDto>;

public class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class CreateTaskHandler : IRequestHandler<CreateTaskCommand, TaskDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;

    public CreateTaskHandler(IApplicationDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<TaskDto> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        // Load the aggregate ROOT. The open-task cap belongs to the project;
        // inserting a TaskItem directly would bypass it.
        var project = await _context.FindProjectAsync(request.ProjectId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var task = project.AddTask(request.Title, request.Description, request.Priority, request.DueDate);

        await _context.SaveChangesAsync(cancellationToken);

        return TaskDto.From(task, _clock.UtcNow);
    }
}

// ---------- Update ----------

public record UpdateTaskCommand(
    Guid Id,
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTimeOffset? DueDate) : IRequest<TaskDto>;

public class UpdateTaskValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class UpdateTaskHandler : IRequestHandler<UpdateTaskCommand, TaskDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;

    public UpdateTaskHandler(IApplicationDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<TaskDto> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.FindTaskAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Task", request.Id);

        task.UpdateDetails(request.Title, request.Description, request.Priority, request.DueDate);
        await _context.SaveChangesAsync(cancellationToken);

        return TaskDto.From(task, _clock.UtcNow);
    }
}

// ---------- Assign ----------

public record AssignTaskCommand(Guid Id, string AssigneeId) : IRequest<TaskDto>;

public class AssignTaskValidator : AbstractValidator<AssignTaskCommand>
{
    public AssignTaskValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AssigneeId).NotEmpty().MaximumLength(200);
    }
}

public class AssignTaskHandler : IRequestHandler<AssignTaskCommand, TaskDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;

    public AssignTaskHandler(IApplicationDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<TaskDto> Handle(AssignTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.FindTaskAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Task", request.Id);

        task.AssignTo(request.AssigneeId);
        await _context.SaveChangesAsync(cancellationToken);

        return TaskDto.From(task, _clock.UtcNow);
    }
}

// ---------- Transition ----------

public record MoveTaskCommand(Guid Id, TaskState State) : IRequest<TaskDto>;

public class MoveTaskValidator : AbstractValidator<MoveTaskCommand>
{
    public MoveTaskValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.State).IsInEnum();
    }
}

public class MoveTaskHandler : IRequestHandler<MoveTaskCommand, TaskDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;

    public MoveTaskHandler(IApplicationDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<TaskDto> Handle(MoveTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.FindTaskAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Task", request.Id);

        // The legal transitions live on the entity, not here. A second caller
        // moving tasks gets the same rules for free.
        task.MoveTo(request.State, _clock.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return TaskDto.From(task, _clock.UtcNow);
    }
}

// ---------- Delete ----------

public record DeleteTaskCommand(Guid Id) : IRequest<Unit>;

public class DeleteTaskHandler : IRequestHandler<DeleteTaskCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public DeleteTaskHandler(IApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.FindTaskAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Task", request.Id);

        var project = await _context.FindProjectAsync(task.ProjectId, cancellationToken)
                      ?? throw new NotFoundException(nameof(Project), task.ProjectId);

        UpdateProjectHandler.EnsureCanModify(project, _currentUser);

        // Cancelling rather than deleting keeps the audit trail intact.
        task.Cancel();
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
