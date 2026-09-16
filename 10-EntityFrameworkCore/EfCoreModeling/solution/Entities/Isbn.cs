namespace EfCoreModeling.Entities;

/// <summary>
/// Part 4: wraps a raw ISBN-10/13 string so "not a valid ISBN" is impossible
/// to represent once construction succeeds -- the validation lives in the
/// domain type, not scattered across every place that touches a Book.
///
/// EF Core has no idea how to store an Isbn column, though: it only speaks
/// primitives (string, int, DateTime, ...) and a handful of well-known types
/// natively. BookConfiguration.cs teaches it with a ValueConverter that
/// unwraps Isbn -> string on the way in and re-validates string -> Isbn on
/// the way out.
/// </summary>
public readonly struct Isbn : IEquatable<Isbn>
{
    public string Value { get; }

    public Isbn(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("An ISBN cannot be empty.", nameof(value));

        var normalized = value.Replace("-", "").Replace(" ", "");

        if (normalized.Length is not (10 or 13) || !normalized.All(char.IsLetterOrDigit))
            throw new ArgumentException($"'{value}' is not a valid ISBN-10/13.", nameof(value));

        Value = normalized;
    }

    public override string ToString() => Value;

    public bool Equals(Isbn other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Isbn other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(Isbn left, Isbn right) => left.Equals(right);
    public static bool operator !=(Isbn left, Isbn right) => !left.Equals(right);
}
