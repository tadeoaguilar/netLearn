namespace EfCoreModeling.Entities;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Isbn Isbn { get; set; }
    public DateOnly PublishedOn { get; set; }
    public Money Price { get; set; } = new(0m, "USD");

    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;

    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
}
