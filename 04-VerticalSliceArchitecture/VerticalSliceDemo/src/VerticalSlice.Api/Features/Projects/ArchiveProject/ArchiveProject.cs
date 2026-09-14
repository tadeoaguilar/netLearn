using MediatR;
using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Common.Database;
using VerticalSlice.Api.Common.Domain;

namespace VerticalSlice.Api.Features.Projects.ArchiveProject;

public record ArchiveProjectCommand(Guid ProjectId) : IRequest<Result<ProjectDto>>;

public class ArchiveProjectHandler : IRequestHandler<ArchiveProjectCommand, Result<ProjectDto>>
{
    private readonly AppDbContext _context;

    public ArchiveProjectHandler(AppDbContext context) => _context = context;

    public async Task<Result<ProjectDto>> Handle(
        ArchiveProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project is null)
        {
            return Result<ProjectDto>.NotFound($"Project {request.ProjectId} was not found.");
        }

        // The rule lives HERE, in the one handler that needs it.
        //
        // In module 03 it lives on the Project entity, so it holds no matter
        // who calls it. Here, a second feature that archives projects would
        // have to remember this check -- that is the cost of the layout.
        if (project.Tasks.Any(t => t.State is TaskState.Todo or TaskState.InProgress))
        {
            return Result<ProjectDto>.Conflict("Cannot archive a project with open tasks.");
        }

        project.IsArchived = true;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ProjectDto>.Success(project.ToDto());
    }
}

public class ArchiveProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/projects/{id:guid}/archive", async (
            Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ArchiveProjectCommand(id), cancellationToken);
            return result.ToHttpResult();
        });
}
