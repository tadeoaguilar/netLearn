using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.DTOs;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Exceptions;

namespace CleanArchitecture.Application.UseCases.Projects;

/// <summary>
/// One class, one use case, one public method.
///
/// The alternative -- a ProjectService with fifteen methods -- is how service
/// classes become thousand-line dumping grounds that nothing can be said about.
/// </summary>
public class CreateProjectUseCase
{
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateProjectUseCase(IProjectRepository projects, IUnitOfWork unitOfWork, IClock clock)
    {
        _projects = projects;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ProjectDto>> ExecuteAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var project = Project.Create(name, _clock.UtcNow);

            await _projects.AddAsync(project, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<ProjectDto>.Success(ProjectDto.From(project));
        }
        catch (DomainException ex)
        {
            // A broken business rule becomes a validation result, not a 500.
            return Result<ProjectDto>.Invalid(ex.Message);
        }
    }
}

public class GetProjectsUseCase
{
    private readonly IProjectRepository _projects;

    public GetProjectsUseCase(IProjectRepository projects) => _projects = projects;

    public async Task<IReadOnlyList<ProjectDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _projects.GetAllAsync(cancellationToken);
        return projects.Select(ProjectDto.From).ToList();
    }
}

public class ArchiveProjectUseCase
{
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;

    public ArchiveProjectUseCase(IProjectRepository projects, IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProjectDto>> ExecuteAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projects.GetByIdAsync(projectId, cancellationToken);

        if (project is null)
        {
            return Result<ProjectDto>.NotFound($"Project {projectId} was not found.");
        }

        try
        {
            project.Archive();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ProjectDto>.Success(ProjectDto.From(project));
        }
        catch (DomainException ex)
        {
            return Result<ProjectDto>.Conflict(ex.Message);
        }
    }
}
