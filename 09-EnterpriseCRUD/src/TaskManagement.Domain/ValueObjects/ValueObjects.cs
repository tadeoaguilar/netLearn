using TaskManagement.Domain.Exceptions;

namespace TaskManagement.Domain.ValueObjects;

/// <summary>
/// A validated, trimmed title. Because it is a value object, an invalid title
/// cannot be constructed, so no handler downstream re-checks it.
/// </summary>
public readonly record struct Title
{
    public const int MaxLength = 200;

    public string Value { get; }

    private Title(string value) => Value = value;

    public static Title Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Title is required.");

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
            throw new DomainException($"Title cannot exceed {MaxLength} characters.");

        return new Title(trimmed);
    }

    public override string ToString() => Value;
    public static implicit operator string(Title title) => title.Value;
}

/// <summary>
/// A user identity from the access token's `sub` claim. A dedicated type stops
/// a user id and a project id being swapped at a call site -- both are strings
/// or Guids otherwise, and the compiler cannot help.
/// </summary>
public readonly record struct UserId
{
    public string Value { get; }

    private UserId(string value) => Value = value;

    public static UserId Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("User id is required.");

        return new UserId(value.Trim());
    }

    public override string ToString() => Value;
    public static implicit operator string(UserId id) => id.Value;
}
