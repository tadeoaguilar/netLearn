using FluentValidation;
using MediatR;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;

namespace TaskManagement.Application.Projects.Commands;

// ---------- Create ----------

public record CreateProjectCommand(string Name, string? Description) : IRequest<ProjectDto>;

public class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class CreateProjectHandler : IRequestHandler<CreateProjectCommand, ProjectDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public CreateProjectHandler(IApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ProjectDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        // Ownership comes from the token, never from the request body. Taking
        // it from the body would let any caller create projects owned by anyone.
        var ownerId = _currentUser.UserId
                      ?? throw new UnauthorizedAccessException("An authenticated user is required.");

        var project = Project.Create(request.Name, request.Description, ownerId);

        _context.AddProject(project);
        await _context.SaveChangesAsync(cancellationToken);

        return ProjectDto.From(project);
    }
}

// ---------- Update ----------

public record UpdateProjectCommand(Guid Id, string Name, string? Description, ProjectStatus Status)
    : IRequest<ProjectDto>;

public class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateProjectHandler : IRequestHandler<UpdateProjectCommand, ProjectDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public UpdateProjectHandler(IApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ProjectDto> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.FindProjectAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException(nameof(Project), request.Id);

        EnsureCanModify(project, _currentUser);

        project.Rename(request.Name);
        project.ChangeDescription(request.Description);
        project.ChangeStatus(request.Status);

        await _context.SaveChangesAsync(cancellationToken);

        return ProjectDto.From(project);
    }

    /// <summary>
    /// AUTHORIZATION, as opposed to authentication. The token proves who you
    /// are; this decides what you may touch. Owners and administrators only.
    /// </summary>
    internal static void EnsureCanModify(Project project, ICurrentUser user)
    {
        if (user.IsInRole("admin")) return;
        if (project.OwnerId == user.UserId) return;

        throw new ForbiddenAccessException($"User may not modify project {project.Id}.");
    }
}

public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }
}

// ---------- Delete ----------

public record DeleteProjectCommand(Guid Id) : IRequest<Unit>;

public class DeleteProjectHandler : IRequestHandler<DeleteProjectCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public DeleteProjectHandler(IApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.FindProjectAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException(nameof(Project), request.Id);

        UpdateProjectHandler.EnsureCanModify(project, _currentUser);

        if (project.OpenTaskCount > 0)
            throw new DomainException($"Cannot delete a project with {project.OpenTaskCount} open task(s).");

        _context.RemoveProject(project);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// ---------- Archive ----------

public record ArchiveProjectCommand(Guid Id) : IRequest<ProjectDto>;

public class ArchiveProjectHandler : IRequestHandler<ArchiveProjectCommand, ProjectDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public ArchiveProjectHandler(IApplicationDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ProjectDto> Handle(ArchiveProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.FindProjectAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException(nameof(Project), request.Id);

        UpdateProjectHandler.EnsureCanModify(project, _currentUser);

        project.Archive();   // the DOMAIN decides whether this is allowed
        await _context.SaveChangesAsync(cancellationToken);

        return ProjectDto.From(project);
    }
}
