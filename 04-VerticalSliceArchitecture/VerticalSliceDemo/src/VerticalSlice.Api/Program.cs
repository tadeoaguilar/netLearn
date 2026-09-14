using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Common.Behaviors;
using VerticalSlice.Api.Common.Database;
using VerticalSlice.Api.Features.Tasks.AssignTask;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? "Data Source=verticalslice.db";

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

// MediatR finds every IRequestHandler in the assembly, so a new slice is
// discovered without editing this file.
builder.Services.AddMediatR(config =>
    config.RegisterServicesFromAssembly(typeof(Program).Assembly));

// FluentValidation finds every AbstractValidator the same way.
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// One pipeline behaviour validates every command, so no handler repeats it.
builder.Services.AddTransient(
    typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITaskNotifier, ConsoleTaskNotifier>();

// Endpoints register themselves -- see Common/IEndpoint.cs.
builder.Services.AddEndpoints();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
}

// A ValidationException from the pipeline becomes a 400 with the field errors.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

    if (feature?.Error is ValidationException validation)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Validation failed",
            status = 400,
            errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
        });
        return;
    }

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { title = "Unexpected error", status = 500 });
}));

app.MapGet("/", () => Results.Ok(new
{
    service = "Vertical Slice Demo",
    note = "Same feature set as 03-CleanArchitecture, organised by feature instead of by layer.",
    endpoints = new[]
    {
        "POST   /projects",
        "GET    /projects",
        "POST   /projects/{id}/archive",
        "POST   /projects/{id}/tasks",
        "POST   /tasks/{id}/assign",
        "POST   /tasks/{id}/complete",
        "GET    /tasks?projectId=&state=&assignee=&minimumPriority="
    }
}));

app.MapEndpoints();

app.Run();

// Exposed for WebApplicationFactory in the integration tests.
public partial class Program;
