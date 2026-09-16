using EfCoreModeling.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreModeling.Tests;

/// <summary>
/// Part 8: the whole schema comes out snake_case. The schema is part of the
/// contract -- anyone writing a migration or a psql query depends on these
/// names -- so it's pinned here, the same way NamingTests pins it in module
/// 09's TaskManagement.Tests.
/// </summary>
public class NamingTests
{
    [Theory]
    [InlineData("Id", "id")]
    [InlineData("Name", "name")]
    [InlineData("PublisherId", "publisher_id")]
    [InlineData("PublishedOn", "published_on")]
    [InlineData("ReviewerName", "reviewer_name")]
    [InlineData("BookType", "book_type")]
    [InlineData("PK_books", "pk_books")]
    [InlineData("IX_books_AuthorId", "ix_books_author_id")]
    [InlineData("FK_reviews_books_BookId", "fk_reviews_books_book_id")]
    public void Names_are_converted_to_snake_case(string input, string expected)
    {
        SnakeCaseNaming.ToSnakeCase(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("TaskDTOId", "task_dto_id")]
    [InlineData("HTTPStatus", "http_status")]
    [InlineData("FileSizeMb", "file_size_mb")]
    public void Runs_of_capitals_are_not_split_letter_by_letter(string input, string expected)
    {
        SnakeCaseNaming.ToSnakeCase(input).Should().Be(expected);
    }

    [Fact]
    public void Table_names_are_snake_case()
    {
        using var context = TestDbContextFactory.Create();

        var tableNames = context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned())
            .Select(e => e.GetTableName())
            .Distinct();

        tableNames.Should().BeEquivalentTo(
            ["authors", "publishers", "genres", "books", "reviews", "book_genres"]);
    }

    [Fact]
    public void Every_mapped_column_is_lower_case_with_no_double_underscores()
    {
        using var context = TestDbContextFactory.Create();

        var columns = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties()
                // The one shadow property SnakeCaseNaming deliberately
                // leaves alone: the ownership-link key of an owned type
                // (Part 3), which is never materialized as its own database
                // column.
                .Where(p => !(entity.IsOwned() && p.IsPrimaryKey())))
            .Select(p => p.GetColumnName())
            .ToArray();

        columns.Should().NotBeEmpty();
        columns.Should().OnlyContain(name => name == name!.ToLowerInvariant());
        columns.Should().NotContain(name => name!.Contains("__"));
    }

    [Fact]
    public void Every_key_foreign_key_and_index_name_is_lower_case()
    {
        using var context = TestDbContextFactory.Create();

        foreach (var entity in context.Model.GetEntityTypes())
        {
            foreach (var key in entity.GetKeys())
            {
                var name = key.GetName();
                if (name is not null)
                {
                    name.Should().Be(name.ToLowerInvariant());
                }
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var name = foreignKey.GetConstraintName();
                if (name is not null)
                {
                    name.Should().Be(name.ToLowerInvariant());
                }
            }

            foreach (var index in entity.GetIndexes())
            {
                var name = index.GetDatabaseName();
                if (name is not null)
                {
                    name.Should().Be(name.ToLowerInvariant());
                }
            }
        }
    }

    [Fact]
    public void The_expected_books_schema_is_produced()
    {
        using var context = TestDbContextFactory.Create();

        var books = context.Model.FindEntityType(typeof(Entities.Book))!;

        books.GetTableName().Should().Be("books");
        books.GetProperties().Select(p => p.GetColumnName()).Should().Contain(
            ["id", "title", "isbn", "published_on", "author_id", "publisher_id", "book_type"]);
    }
}
