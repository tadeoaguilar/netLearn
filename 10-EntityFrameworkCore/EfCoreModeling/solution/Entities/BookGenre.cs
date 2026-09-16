namespace EfCoreModeling.Entities;

/// <summary>
/// Part 5: the explicit join entity for the Book &lt;-&gt; Genre many-to-many.
///
/// EF Core 5+ can infer a join table automatically for a plain many-to-many
/// (`HasMany(b => b.Genres).WithMany(g => g.Books)`), and hides the join
/// table from you entirely. That shortcut has no room for AddedAt, though --
/// the moment a many-to-many needs even one extra column, the implicit
/// skip-navigation table stops being an option and you model the join table
/// as a first-class entity, exactly like this one.
/// </summary>
public class BookGenre
{
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;

    public DateTime AddedAt { get; set; }
}
