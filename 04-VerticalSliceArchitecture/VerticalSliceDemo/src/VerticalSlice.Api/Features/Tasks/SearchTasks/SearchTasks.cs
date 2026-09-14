using MediatR;
using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Common.Database;
using VerticalSlice.Api.Common.Domain;

namespace VerticalSlice.Api.Features.Tasks.SearchTasks;

public record SearchTasksQuery(
    Guid? ProjectId = null,
    TaskState? State = null,
    string? Assignee = null,
    Priority? MinimumPriority = null) : IRequest<IReadOnlyList<TaskDto>>;

public class SearchTasksHandler : IRequestHandler<SearchTasksQuery, IReadOnlyList<TaskDto>>
{
    private readonly AppDbContext _context;

    public SearchTasksHandler(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<TaskDto>> Handle(
        SearchTasksQuery request, CancellationToken cancellationToken)
    {
        // Note there is no TaskFilter type and no repository interface: the
        // query is written where it is used. Module 03 needs the extra
        // abstraction so Application can stay ignorant of EF Core; here the
        // handler is already the outermost layer, so there is nothing to hide.
        var query = _context.Tasks.AsNoTracking().AsQueryable();

        if (request.ProjectId is { } projectId) query = query.Where(t => t.ProjectId == projectId);
        if (request.State is { } state) query = query.Where(t => t.State == state);
        if (!string.IsNullOrWhiteSpace(request.Assignee)) query = query.Where(t => t.Assignee == request.Assignee);
        if (request.MinimumPriority is { } minimum) query = query.Where(t => t.Priority >= minimum);

        var tasks = await query
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return tasks.Select(t => t.ToDto()).ToList();
    }
}

public class SearchTasksEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/tasks", async (
            ISender sender,
            CancellationToken cancellationToken,
            Guid? projectId = null,
            TaskState? state = null,
            string? assignee = null,
            Priority? minimumPriority = null) =>
        {
            var query = new SearchTasksQuery(projectId, state, assignee, minimumPriority);
            return Results.Ok(await sender.Send(query, cancellationToken));
        });
}
