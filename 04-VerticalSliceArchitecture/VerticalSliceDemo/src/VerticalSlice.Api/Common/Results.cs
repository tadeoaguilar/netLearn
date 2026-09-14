namespace VerticalSlice.Api.Common;

/// <summary>
/// Shared between slices because HTTP status mapping is the same everywhere.
/// Note that this is a much smaller shared surface than module 03's
/// Application layer: slices share plumbing here, not business logic.
/// </summary>
public readonly record struct Result<T>
{
    private Result(T? value, ResultError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public ResultError? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> NotFound(string message) => new(default, new ResultError(ResultErrorKind.NotFound, message));
    public static Result<T> Conflict(string message) => new(default, new ResultError(ResultErrorKind.Conflict, message));
}

public enum ResultErrorKind { NotFound, Conflict }

public record ResultError(ResultErrorKind Kind, string Message);

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess?.Invoke(result.Value!) ?? Results.Ok(result.Value);
        }

        var error = result.Error!;
        return error.Kind switch
        {
            ResultErrorKind.NotFound => Results.Problem(error.Message, statusCode: StatusCodes.Status404NotFound),
            ResultErrorKind.Conflict => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(error.Message, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}

/// <summary>Shapes returned to clients. Shared so slices agree on the contract.</summary>
public record TaskDto(
    Guid Id, Guid ProjectId, string Title, string? Description,
    string Priority, string State, string? Assignee,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);

public record ProjectDto(
    Guid Id, string Name, bool IsArchived, int TotalTasks, int OpenTasks, DateTimeOffset CreatedAt);

public static class Mapping
{
    public static TaskDto ToDto(this Domain.TaskItem task) => new(
        task.Id, task.ProjectId, task.Title, task.Description,
        task.Priority.ToString(), task.State.ToString(), task.Assignee,
        task.CreatedAt, task.CompletedAt);

    public static ProjectDto ToDto(this Domain.Project project) => new(
        project.Id, project.Name, project.IsArchived,
        project.Tasks.Count,
        project.Tasks.Count(t => t.State is Domain.TaskState.Todo or Domain.TaskState.InProgress),
        project.CreatedAt);
}
