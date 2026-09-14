using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Projects.Commands;
using TaskManagement.Domain.Exceptions;

namespace TaskManagement.WebApi.Middleware;

/// <summary>
/// Translates exceptions into RFC 7807 problem responses.
///
/// This mapping is the contract between the domain's vocabulary and HTTP's.
/// The domain knows "you broke a rule" and "that does not exist"; it has never
/// heard of 409 or 404, and should not.
/// </summary>
public class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public ProblemDetailsExceptionHandler(
        ILogger<ProblemDetailsExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, extensions) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            // Only genuine faults are logged as errors. Logging every 404 at
            // error level is how real problems get buried.
            _logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            _logger.LogInformation("Request rejected: {Title}", title);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Instance = context.Request.Path,

            // Never leak a stack trace to a client outside development.
            Detail = status >= StatusCodes.Status500InternalServerError && !_environment.IsDevelopment()
                ? "An unexpected error occurred."
                : exception.Message
        };

        foreach (var (key, value) in extensions) problem.Extensions[key] = value;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    private static (int Status, string Title, Dictionary<string, object?> Extensions) Map(Exception exception) =>
        exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                new Dictionary<string, object?>
                {
                    ["errors"] = validation.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
                }),

            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", new()),

            // A broken business rule is a 409, not a 500. The request was
            // well-formed; the system simply refuses it.
            DomainException => (StatusCodes.Status409Conflict, "Business rule violated", new()),

            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden", new()),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", new()),

            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", new())
        };
}
