namespace EfCoreModeling.Entities;

/// <summary>
/// A plain, persistence-ignorant POCO. Nothing on this class knows it will
/// end up as a Postgres row -- no [Required], no [MaxLength], no base class
/// from EF Core. All of that lives in Configurations/AuthorConfiguration.cs
/// instead. See Part 1 of EXERCISE.md for why.
/// </summary>
public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }

    public ICollection<Book> Books { get; set; } = new List<Book>();
}
