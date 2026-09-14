using MediatR;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;

namespace TaskManagement.Application.Projects.Queries;

public record GetProjectQuery(Guid Id) : IRequest<ProjectDto>;

public class GetProjectHandler : IRequestHandler<GetProjectQuery, ProjectDto>
{
    private readonly IApplicationDbContext _context;

    public GetProjectHandler(IApplicationDbContext context) => _context = context;

    public async Task<ProjectDto> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        var project = await _context.FindProjectAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException("Project", request.Id);

        return ProjectDto.From(project);
    }
}

public record GetProjectsQuery(
    ProjectStatus? Status = null,
    string? OwnerId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<ProjectDto>>;

public class GetProjectsHandler : IRequestHandler<GetProjectsQuery, PagedResult<ProjectDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProjectsHandler(IApplicationDbContext context) => _context = context;

    public Task<PagedResult<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Projects;

        if (request.Status is { } status) query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(request.OwnerId)) query = query.Where(p => p.OwnerId == request.OwnerId);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);   // never trust a client's page size

        var ordered = query.OrderBy(p => p.Name).ToList();

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProjectDto.From)
            .ToList();

        return Task.FromResult(new PagedResult<ProjectDto>(items, page, pageSize, ordered.Count));
    }
}
