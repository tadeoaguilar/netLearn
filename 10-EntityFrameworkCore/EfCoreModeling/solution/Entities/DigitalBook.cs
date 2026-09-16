namespace EfCoreModeling.Entities;

/// <summary>
/// Part 6: a table-per-hierarchy (TPH) subtype. DigitalBook rows live in the
/// same "books" table as plain Book rows -- there is no "digital_books"
/// table -- distinguished only by a discriminator column that
/// BookConfiguration.cs sets up via HasDiscriminator. FileSizeMb and Format
/// are simply NULL on every row that isn't a DigitalBook.
/// </summary>
public class DigitalBook : Book
{
    public double FileSizeMb { get; set; }
    public string Format { get; set; } = string.Empty;
}
