namespace EfCoreMigrations.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public int AuthorId { get; set; }
    public Author? Author { get; set; }

    public int Pages { get; set; }
    public int? Rating { get; set; }

    public int? GenreId { get; set; }
    public Genre? Genre { get; set; }
}
