using MediatR;
using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Common.Database;

namespace VerticalSlice.Api.Features.Projects.ListProjects;

public record ListProjectsQuery : IRequest<IReadOnlyList<ProjectDto>>;

public class ListProjectsHandler : IRequestHandler<ListProjectsQuery, IReadOnlyList<ProjectDto>>
{
    private readonly AppDbContext _context;

    public ListProjectsHandler(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<ProjectDto>> Handle(
        ListProjectsQuery request, CancellationToken cancellationToken)
    {
        // A read slice can shape its query for exactly what it returns. There
        // is no shared repository method to compromise with -- the freedom to
        // optimise per-feature is one of the real advantages here.
        var projects = await _context.Projects
            .AsNoTracking()
            .Include(p => p.Tasks)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return projects.Select(p => p.ToDto()).ToList();
    }
}

public class ListProjectsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/projects", async (ISender sender, CancellationToken cancellationToken)
            => Results.Ok(await sender.Send(new ListProjectsQuery(), cancellationToken)));
}
