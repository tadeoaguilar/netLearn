namespace EfCoreQuerying.Domain;

public class Publisher
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<Book> Books { get; set; } = [];
}
