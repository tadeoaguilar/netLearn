namespace EfCoreModeling.Entities;

public class Review
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    // 1-5, enforced by a database CHECK constraint (Part 7) rather than only
    // in C# -- anything that writes to this table, migration scripts and
    // psql included, gets the same guarantee.
    public int Rating { get; set; }

    public string? Comment { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
}
