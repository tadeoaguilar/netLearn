namespace EfCoreModeling.Entities;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // No direct ICollection<Book> here. The relationship to Book always goes
    // through BookGenre (see Part 5), which is exactly the point: the payload
    // (AddedAt) has nowhere to live on a plain many-to-many navigation.
    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
}
