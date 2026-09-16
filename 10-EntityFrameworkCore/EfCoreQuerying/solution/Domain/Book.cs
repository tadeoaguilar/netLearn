namespace EfCoreQuerying.Domain;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public DateOnly PublishedOn { get; set; }
    public decimal Price { get; set; }

    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;

    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;

    /// <summary>
    /// Maps to a native Postgres <c>text[]</c> column -- the Npgsql provider
    /// understands array CLR types out of the box, no value converter needed.
    /// </summary>
    public string[] Tags { get; set; } = [];

    /// <summary>
    /// Free-form attributes such as <c>{"language":"en","pages":320}</c>,
    /// stored as a native Postgres <c>jsonb</c> column and mapped as plain
    /// text here. A <c>JsonDocument</c> property would also map to jsonb,
    /// but it is IDisposable and needs a hand-written value comparer to
    /// behave correctly under change tracking -- storing (and parsing on
    /// demand with System.Text.Json) the JSON text is simpler and just as
    /// queryable from LINQ via <c>EF.Functions.JsonContains</c> and friends.
    /// </summary>
    public string Metadata { get; set; } = "{}";

    public List<Review> Reviews { get; set; } = [];
    public List<Genre> Genres { get; set; } = [];
}
