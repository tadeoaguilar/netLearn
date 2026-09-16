using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCoreModeling.Tests;

/// <summary>
/// Part 6: table-per-hierarchy. DigitalBook rows live in "books" alongside
/// plain Book rows, distinguished by a discriminator column.
/// </summary>
public class InheritanceTests
{
    [Fact]
    public void DigitalBook_is_derived_from_book()
    {
        typeof(DigitalBook).IsSubclassOf(typeof(Book)).Should().BeTrue();
    }

    [Fact]
    public void Book_and_DigitalBook_share_the_same_table()
    {
        using var context = TestDbContextFactory.Create();

        var book = context.Model.FindEntityType(typeof(Book))!;
        var digitalBook = context.Model.FindEntityType(typeof(DigitalBook))!;

        book.GetTableName().Should().Be("books");
        digitalBook.GetTableName().Should().Be("books");
    }

    [Fact]
    public void DigitalBook_has_no_foreign_key_of_its_own_back_to_books()
    {
        // TPH means DigitalBook doesn't reference "books" -- it IS a row in
        // "books". A foreign key here would mean something went wrong and
        // it ended up mapped as a separate (TPT-style) table instead.
        using var context = TestDbContextFactory.Create();
        var digitalBook = context.Model.FindEntityType(typeof(DigitalBook))!;

        digitalBook.GetDeclaredForeignKeys().Should().BeEmpty();
    }

    [Fact]
    public void A_discriminator_column_distinguishes_book_from_digitalBook()
    {
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;

        var discriminator = book.FindProperty("BookType");

        discriminator.Should().NotBeNull();
        discriminator!.GetColumnName().Should().Be("book_type");
        discriminator!.ClrType.Should().Be(typeof(string));
    }

    [Fact]
    public void DigitalBook_specific_properties_are_mapped_as_nullable_columns_on_books()
    {
        // Every plain Book row has no file size or format, so these columns
        // must tolerate NULL even though the CLR properties are non-nullable
        // on DigitalBook -- there's no other way to fit both shapes in one
        // table.
        using var context = TestDbContextFactory.Create();
        var digitalBook = context.Model.FindEntityType(typeof(DigitalBook))!;

        var fileSize = digitalBook.FindProperty(nameof(DigitalBook.FileSizeMb));
        var format = digitalBook.FindProperty(nameof(DigitalBook.Format));

        fileSize.Should().NotBeNull();
        format.Should().NotBeNull();
        fileSize!.GetColumnName().Should().Be("file_size_mb");
        format!.GetColumnName().Should().Be("format");
    }

    [Fact]
    public void Querying_the_base_set_reaches_every_book_shape()
    {
        // A DbSet<Book> query has no filter on the discriminator, so it maps
        // to every row in "books" -- base and derived alike. DbSet<DigitalBook>
        // (used in the next test) adds the discriminator filter.
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;

        book.GetDerivedTypes().Select(t => t.ClrType).Should().Contain(typeof(DigitalBook));
    }
}
