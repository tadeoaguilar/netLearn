using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCoreModeling.Tests;

/// <summary>
/// Part 2 (one-to-many) and Part 5 (explicit many-to-many with a payload).
/// </summary>
public class RelationshipTests
{
    [Fact]
    public void Book_has_a_foreign_key_to_author()
    {
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;

        var fk = book.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(Author));

        fk.Properties.Select(p => p.Name).Should().BeEquivalentTo([nameof(Book.AuthorId)]);
        fk.IsUnique.Should().BeFalse("one author writes many books");
    }

    [Fact]
    public void Book_has_a_foreign_key_to_publisher()
    {
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;

        var fk = book.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(Publisher));

        fk.Properties.Select(p => p.Name).Should().BeEquivalentTo([nameof(Book.PublisherId)]);
        fk.IsUnique.Should().BeFalse("one publisher publishes many books");
    }

    [Fact]
    public void Author_and_Publisher_expose_the_inverse_collection_of_books()
    {
        using var context = TestDbContextFactory.Create();

        var author = context.Model.FindEntityType(typeof(Author))!;
        var publisher = context.Model.FindEntityType(typeof(Publisher))!;

        author.FindNavigation(nameof(Author.Books)).Should().NotBeNull();
        publisher.FindNavigation(nameof(Publisher.Books)).Should().NotBeNull();
    }

    [Fact]
    public void Review_has_a_cascading_foreign_key_to_book()
    {
        using var context = TestDbContextFactory.Create();
        var review = context.Model.FindEntityType(typeof(Review))!;

        var fk = review.GetForeignKeys().Single();

        fk.PrincipalEntityType.ClrType.Should().Be(typeof(Book));
        fk.Properties.Select(p => p.Name).Should().BeEquivalentTo([nameof(Review.BookId)]);
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade, "a deleted book takes its reviews with it");
    }

    [Fact]
    public void BookGenre_has_a_composite_primary_key_of_book_and_genre()
    {
        using var context = TestDbContextFactory.Create();
        var bookGenre = context.Model.FindEntityType(typeof(BookGenre))!;

        var primaryKey = bookGenre.FindPrimaryKey()!;

        primaryKey.Properties.Select(p => p.Name)
            .Should().BeEquivalentTo([nameof(BookGenre.BookId), nameof(BookGenre.GenreId)]);
    }

    [Fact]
    public void BookGenre_has_foreign_keys_to_both_book_and_genre()
    {
        using var context = TestDbContextFactory.Create();
        var bookGenre = context.Model.FindEntityType(typeof(BookGenre))!;

        var principals = bookGenre.GetForeignKeys().Select(fk => fk.PrincipalEntityType.ClrType).ToArray();

        principals.Should().BeEquivalentTo([typeof(Book), typeof(Genre)]);
    }

    [Fact]
    public void BookGenre_carries_the_addedAt_payload_column()
    {
        // The whole reason this is an explicit join entity instead of an
        // implicit EF Core skip-navigation table: there is somewhere for
        // AddedAt to live.
        using var context = TestDbContextFactory.Create();
        var bookGenre = context.Model.FindEntityType(typeof(BookGenre))!;

        var addedAt = bookGenre.FindProperty(nameof(BookGenre.AddedAt));

        addedAt.Should().NotBeNull();
        addedAt!.ClrType.Should().Be(typeof(DateTime));
    }

    [Fact]
    public void Book_and_Genre_are_not_directly_linked_by_a_skip_navigation()
    {
        // If EF Core had inferred an implicit many-to-many, Book would carry
        // a skip navigation straight to Genre. It doesn't -- everything goes
        // through the explicit BookGenre join entity instead.
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;

        book.GetSkipNavigations().Should().BeEmpty();
    }
}
