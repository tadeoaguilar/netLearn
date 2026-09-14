using CleanArchitecture.Domain.Exceptions;

namespace CleanArchitecture.Domain.ValueObjects;

/// <summary>
/// A validated title. Being a value object means an invalid title cannot exist:
/// there is no way to construct one that is empty or over-long, so no code
/// downstream has to re-check it.
/// </summary>
public readonly record struct TaskTitle
{
    public const int MaxLength = 200;

    public string Value { get; }

    private TaskTitle(string value) => Value = value;

    public static TaskTitle Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Task title is required.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new DomainException($"Task title cannot exceed {MaxLength} characters.");
        }

        return new TaskTitle(trimmed);
    }

    public override string ToString() => Value;

    public static implicit operator string(TaskTitle title) => title.Value;
}
