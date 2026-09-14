using CleanArchitecture.Application;
using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.UseCases.Projects;
using CleanArchitecture.Application.UseCases.Tasks;
using CleanArchitecture.Domain.Enums;
using CleanArchitecture.Infrastructure;
using CleanArchitecture.Infrastructure.Persistence;
using CleanArchitecture.WebApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? "Data Source=cleanarchitecture.db";

// The composition root, in two lines. Each layer registers itself.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

// Accept and emit enums as names ("High") rather than ordinals (2). Without
// this, System.Text.Json rejects {"priority":"High"} with an empty 400, and
// the contract silently depends on the ORDER of the enum members -- reorder
// Priority one day and every stored client request means something else.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

// Create the database on startup. A real deployment would run migrations.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.MapGet("/", () => Results.Ok(new
{
    service = "Clean Architecture Demo",
    endpoints = new[]
    {
        "POST   /projects",
        "GET    /projects",
        "POST   /projects/{id}/archive",
        "POST   /projects/{id}/tasks",
        "POST   /tasks/{id}/assign",
        "POST   /tasks/{id}/complete",
        "GET    /tasks?projectId=&state=&assignee="
    }
}));

app.MapPost("/projects", async (
    CreateProjectRequest request,
    CreateProjectUseCase useCase,
    CancellationToken cancellationToken) =>
{
    // The endpoint does three things: bind input, call one use case, map the
    // result. Any business logic here would be logic the tests cannot reach
    // without going through HTTP.
    var result = await useCase.ExecuteAsync(request.Name, cancellationToken);
    return result.ToHttpResult(dto => Results.Created($"/projects/{dto.Id}", dto));
});

app.MapGet("/projects", async (GetProjectsUseCase useCase, CancellationToken cancellationToken)
    => Results.Ok(await useCase.ExecuteAsync(cancellationToken)));

app.MapPost("/projects/{id:guid}/archive", async (
    Guid id, ArchiveProjectUseCase useCase, CancellationToken cancellationToken) =>
{
    var result = await useCase.ExecuteAsync(id, cancellationToken);
    return result.ToHttpResult();
});

app.MapPost("/projects/{id:guid}/tasks", async (
    Guid id,
    CreateTaskRequest request,
    CreateTaskUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var result = await useCase.ExecuteAsync(
        id, request.Title, request.Description, request.Priority, cancellationToken);
    return result.ToHttpResult(dto => Results.Created($"/tasks/{dto.Id}", dto));
});

app.MapPost("/tasks/{id:guid}/assign", async (
    Guid id,
    AssignTaskRequest request,
    AssignTaskUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var result = await useCase.ExecuteAsync(id, request.Assignee, cancellationToken);
    return result.ToHttpResult();
});

app.MapPost("/tasks/{id:guid}/complete", async (
    Guid id, CompleteTaskUseCase useCase, CancellationToken cancellationToken) =>
{
    var result = await useCase.ExecuteAsync(id, cancellationToken);
    return result.ToHttpResult();
});

app.MapGet("/tasks", async (
    SearchTasksUseCase useCase,
    CancellationToken cancellationToken,
    Guid? projectId = null,
    TaskState? state = null,
    string? assignee = null,
    Priority? minimumPriority = null) =>
{
    var filter = new TaskFilter(projectId, state, assignee, minimumPriority);
    return Results.Ok(await useCase.ExecuteAsync(filter, cancellationToken));
});

app.Run();

// Exposed so the integration tests can spin this API up with WebApplicationFactory.
public partial class Program;
