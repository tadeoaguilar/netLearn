using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Common.Database;
using VerticalSlice.Api.Common.Domain;

namespace VerticalSlice.Api.Features.Projects.CreateProject;

/// <summary>
/// ONE FILE, one feature. Command, validator, handler and endpoint together.
///
/// Compare module 03, where creating a project touches Project.cs (Domain),
/// IProjectRepository.cs + CreateProjectUseCase.cs + ProjectDto.cs
/// (Application), ProjectRepository.cs (Infrastructure) and Program.cs
/// (WebApi) -- six files across four projects for the same behaviour.
/// </summary>
public record CreateProjectCommand(string Name) : IRequest<ProjectDto>;

public class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        // Validation is declarative and sits next to the command it validates.
        // In module 03 this rule lives inside the entity's factory method.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(200);
    }
}

public class CreateProjectHandler : IRequestHandler<CreateProjectCommand, ProjectDto>
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _time;

    public CreateProjectHandler(AppDbContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    public async Task<ProjectDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        // The handler talks to the DbContext directly. No repository, no unit
        // of work, no port. For a slice this small those layers would be
        // indirection with nothing behind it.
        var project = new Project
        {
            Name = request.Name.Trim(),
            CreatedAt = _time.GetUtcNow()
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);

        return project.ToDto();
    }
}

public class CreateProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/projects", async (
            CreateProjectCommand command, ISender sender, CancellationToken cancellationToken) =>
        {
            var project = await sender.Send(command, cancellationToken);
            return Results.Created($"/projects/{project.Id}", project);
        });
}
