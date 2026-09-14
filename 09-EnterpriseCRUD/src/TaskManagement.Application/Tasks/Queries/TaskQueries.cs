using MediatR;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;

namespace TaskManagement.Application.Tasks.Queries;

public record GetTaskQuery(Guid Id) : IRequest<TaskDto>;

public class GetTaskHandler : IRequestHandler<GetTaskQuery, TaskDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;

    public GetTaskHandler(IApplicationDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<TaskDto> Handle(GetTaskQuery request, CancellationToken cancellationToken)
    {
        var task = await _context.FindTaskAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Task", request.Id);

        return TaskDto.From(task, _clock.UtcNow);
    }
}

public record GetTasksQuery(
    Guid? ProjectId = null,
    TaskState? State = null,
    TaskPriority? MinimumPriority = null,
    string? AssigneeId = null,
    bool? OverdueOnly = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<TaskDto>>;

public class GetTasksHandler : IRequestHandler<GetTasksQuery, PagedResult<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;

    public GetTasksHandler(IApplicationDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public Task<PagedResult<TaskDto>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var query = _context.Tasks;

        if (request.ProjectId is { } projectId) query = query.Where(t => t.ProjectId == projectId);
        if (request.State is { } state) query = query.Where(t => t.State == state);
        if (request.MinimumPriority is { } priority) query = query.Where(t => t.Priority >= priority);
        if (!string.IsNullOrWhiteSpace(request.AssigneeId)) query = query.Where(t => t.AssigneeId == request.AssigneeId);

        var materialised = query
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        // IsOverdue compares against "now", so it is evaluated after the query
        // rather than translated into SQL.
        if (request.OverdueOnly == true)
        {
            materialised = materialised.Where(t => t.IsOverdue(now)).ToList();
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = materialised
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => TaskDto.From(t, now))
            .ToList();

        return Task.FromResult(new PagedResult<TaskDto>(items, page, pageSize, materialised.Count));
    }
}
