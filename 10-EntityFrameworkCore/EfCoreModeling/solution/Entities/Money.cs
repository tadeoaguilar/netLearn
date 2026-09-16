namespace EfCoreModeling.Entities;

/// <summary>
/// Part 3: a value object with no identity of its own -- two Money instances
/// with the same Amount and Currency are interchangeable, and neither one is
/// ever looked up by an Id. EF Core maps it as an OWNED TYPE: its columns
/// live inline on the owner's table, and it can never be queried or saved
/// independently of the Book that owns it (see BookConfiguration.OwnsOne).
/// </summary>
public record Money(decimal Amount, string Currency);
