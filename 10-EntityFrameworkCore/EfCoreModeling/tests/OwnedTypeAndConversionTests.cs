using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCoreModeling.Tests;

/// <summary>
/// Part 3 (owned type) and Part 4 (value converter).
/// </summary>
public class OwnedTypeAndConversionTests
{
    [Fact]
    public void Book_declares_an_owned_navigation_to_price()
    {
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;

        var priceNavigation = book.FindNavigation(nameof(Book.Price));

        priceNavigation.Should().NotBeNull();
        priceNavigation!.TargetEntityType.IsOwned().Should().BeTrue();
    }

    [Fact]
    public void Price_owned_type_maps_to_amount_and_currency_columns()
    {
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;

        var priceType = book.FindNavigation(nameof(Book.Price))!.TargetEntityType;

        var columnNames = priceType.GetProperties()
            .Where(p => !p.IsPrimaryKey()) // the ownership-link shadow key isn't a real column
            .Select(p => p.GetColumnName());

        columnNames.Should().BeEquivalentTo(["amount", "currency"]);
    }

    [Fact]
    public void Price_owned_type_shares_the_books_table_rather_than_having_its_own()
    {
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;
        var priceType = book.FindNavigation(nameof(Book.Price))!.TargetEntityType;

        priceType.GetTableName().Should().Be(book.GetTableName());
    }

    [Fact]
    public void Isbn_property_is_mapped_through_a_value_converter()
    {
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;

        var isbn = book.FindProperty(nameof(Book.Isbn))!;

        isbn.ClrType.Should().Be(typeof(Isbn), "the entity keeps the validated wrapper type in C#");
        isbn.GetValueConverter().Should().NotBeNull();
        isbn.GetValueConverter()!.ProviderClrType.Should().Be(typeof(string), "only a primitive can be stored");
    }

    [Fact]
    public void Book_isbn_has_a_unique_index()
    {
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;

        var isbn = book.FindProperty(nameof(Book.Isbn))!;

        book.GetIndexes().Should().Contain(i => i.IsUnique && i.Properties.SequenceEqual(new[] { isbn }));
    }

    [Theory]
    [InlineData("0-13-468599-7")]
    [InlineData("9780134685991")]
    public void Isbn_accepts_valid_ISBN_10_and_13_values(string value)
    {
        var isbn = new Isbn(value);

        isbn.Value.Should().NotContain("-");
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("12345")]
    public void Isbn_rejects_invalid_values(string value)
    {
        var act = () => new Isbn(value);

        act.Should().Throw<ArgumentException>();
    }
}
