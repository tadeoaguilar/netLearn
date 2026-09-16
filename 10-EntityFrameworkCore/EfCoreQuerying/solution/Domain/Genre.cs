namespace EfCoreQuerying.Domain;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Implicit many-to-many with Book -- EF Core generates the join table
    // (book_genre) for us since neither side needs a payload column.
    public List<Book> Books { get; set; } = [];
}
