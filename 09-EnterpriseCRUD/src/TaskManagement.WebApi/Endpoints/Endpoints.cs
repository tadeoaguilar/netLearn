using MediatR;
using TaskManagement.Application.Common.Models;
using TaskManagement.Application.Projects.Commands;
using TaskManagement.Application.Projects.Queries;
using TaskManagement.Application.Tasks.Commands;
using TaskManagement.Application.Tasks.Queries;
using TaskManagement.Domain.Enums;

namespace TaskManagement.WebApi.Endpoints;

public record CreateProjectRequest(string Name, string? Description);
public record UpdateProjectRequest(string Name, string? Description, ProjectStatus Status);
public record CreateTaskRequest(string Title, string? Description, TaskPriority Priority = TaskPriority.Normal, DateTimeOffset? DueDate = null);
public record UpdateTaskRequest(string Title, string? Description, TaskPriority Priority, DateTimeOffset? DueDate);
public record AssignTaskRequest(string AssigneeId);
public record MoveTaskRequest(TaskState State);

/// <summary>
/// Endpoints are thin by design: bind, send one request through MediatR, return.
///
/// Anything more here would be logic the tests can only reach over HTTP.
/// Exceptions are handled centrally, so there is not a try/catch in sight.
/// </summary>
public static class Endpoints
{
    public static void MapApi(this WebApplication app)
    {
        // v1 is explicit in the route from day one. Retrofitting versioning
        // after clients exist is considerably harder than starting with it.
        var api = app.MapGroup("/api/v1").RequireAuthorization();

        MapProjects(api);
        MapTasks(api);
    }

    private static void MapProjects(RouteGroupBuilder api)
    {
        var projects = api.MapGroup("/projects").WithTags("Projects");

        projects.MapPost("/", async (CreateProjectRequest request, ISender sender, CancellationToken ct) =>
        {
            var project = await sender.Send(new CreateProjectCommand(request.Name, request.Description), ct);
            return Results.Created($"/api/v1/projects/{project.Id}", project);
        });

        projects.MapGet("/", async (
            ISender sender, CancellationToken ct,
            ProjectStatus? status = null, string? ownerId = null, int page = 1, int pageSize = 20)
            => Results.Ok(await sender.Send(new GetProjectsQuery(status, ownerId, page, pageSize), ct)));

        projects.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new GetProjectQuery(id), ct)));

        projects.MapPut("/{id:guid}", async (
            Guid id, UpdateProjectRequest request, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(
                new UpdateProjectCommand(id, request.Name, request.Description, request.Status), ct)));

        projects.MapPost("/{id:guid}/archive", async (Guid id, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new ArchiveProjectCommand(id), ct)));

        projects.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteProjectCommand(id), ct);
            return Results.NoContent();
        });

        projects.MapPost("/{id:guid}/tasks", async (
            Guid id, CreateTaskRequest request, ISender sender, CancellationToken ct) =>
        {
            var task = await sender.Send(new CreateTaskCommand(
                id, request.Title, request.Description, request.Priority, request.DueDate), ct);

            return Results.Created($"/api/v1/tasks/{task.Id}", task);
        });
    }

    private static void MapTasks(RouteGroupBuilder api)
    {
        var tasks = api.MapGroup("/tasks").WithTags("Tasks");

        tasks.MapGet("/", async (
            ISender sender, CancellationToken ct,
            Guid? projectId = null, TaskState? state = null, TaskPriority? minimumPriority = null,
            string? assigneeId = null, bool? overdueOnly = null, int page = 1, int pageSize = 20)
            => Results.Ok(await sender.Send(new GetTasksQuery(
                projectId, state, minimumPriority, assigneeId, overdueOnly, page, pageSize), ct)));

        tasks.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new GetTaskQuery(id), ct)));

        tasks.MapPut("/{id:guid}", async (
            Guid id, UpdateTaskRequest request, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new UpdateTaskCommand(
                id, request.Title, request.Description, request.Priority, request.DueDate), ct)));

        tasks.MapPost("/{id:guid}/assign", async (
            Guid id, AssignTaskRequest request, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new AssignTaskCommand(id, request.AssigneeId), ct)));

        tasks.MapPost("/{id:guid}/move", async (
            Guid id, MoveTaskRequest request, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new MoveTaskCommand(id, request.State), ct)));

        tasks.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteTaskCommand(id), ct);
            return Results.NoContent();
        });
    }
}
