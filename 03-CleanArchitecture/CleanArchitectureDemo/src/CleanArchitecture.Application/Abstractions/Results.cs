namespace CleanArchitecture.Application.Abstractions;

/// <summary>
/// A use case's outcome, without exceptions for expected failures.
///
/// "Project not found" is an ordinary result, not an exceptional one. Modelling
/// it as a value keeps the happy path readable and lets the API layer map each
/// kind of failure to the right status code.
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
    public static Result<T> Invalid(string message) => new(default, new ResultError(ResultErrorKind.Validation, message));
    public static Result<T> Conflict(string message) => new(default, new ResultError(ResultErrorKind.Conflict, message));
}

public enum ResultErrorKind
{
    NotFound,
    Validation,
    Conflict
}

public record ResultError(ResultErrorKind Kind, string Message);
