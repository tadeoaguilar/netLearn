namespace EfCoreQuerying.Domain;

public class Review
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    /// <summary>1 to 5, enforced by a check constraint in OnModelCreating.</summary>
    public int Rating { get; set; }

    public string? Comment { get; set; }
}
