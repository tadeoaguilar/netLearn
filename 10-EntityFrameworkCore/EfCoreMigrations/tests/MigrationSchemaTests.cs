using System.Data.Common;
using EfCoreMigrations.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EfCoreMigrations.Tests;

/// <summary>
/// Proves the migration history actually produces the schema EXERCISE.md
/// describes -- not "the code compiles" but "PostgreSQL agrees this is the
/// shape of the database". Needs Docker (see PostgresFixture); there's no
/// way to run these without it.
/// </summary>
public class MigrationSchemaTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public MigrationSchemaTests(PostgresFixture fixture) => _fixture = fixture;

    private static async Task<List<string>> GetColumnsAsync(DbConnection connection, string table)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name = @table";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "table";
        parameter.Value = table;
        cmd.Parameters.Add(parameter);

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
        return columns;
    }

    [Fact]
    public async Task The_five_expected_migrations_are_recorded_in_order()
    {
        await using var context = _fixture.CreateContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();

        // Migration ids are "yyyyMMddHHmmss_Name" -- 14 digits, an
        // underscore, then the name -- and GetAppliedMigrationsAsync
        // returns them in the order they were applied.
        applied.Select(id => id[15..]).Should().Equal(
            "InitialCreate",
            "AddBookPageCount",
            "RenameBookPagesColumn",
            "AddIndexAndBookRatingConstraint",
            "SeedGenreReferenceData");
    }

    [Fact]
    public async Task The_authors_table_has_the_expected_snake_case_columns()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.OpenConnectionAsync();

        var columns = await GetColumnsAsync(context.Database.GetDbConnection(), "authors");

        columns.Should().BeEquivalentTo("id", "name");
    }

    [Fact]
    public async Task The_books_table_has_the_expected_columns_after_all_five_migrations()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.OpenConnectionAsync();

        var columns = await GetColumnsAsync(context.Database.GetDbConnection(), "books");

        // "pages", not "page_count" -- proves the rename migration replaced
        // the column rather than sitting alongside a leftover.
        columns.Should().BeEquivalentTo("id", "title", "author_id", "pages", "rating", "genre_id");
        columns.Should().NotContain("page_count");
    }

    [Fact]
    public async Task The_genres_table_has_the_expected_columns()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.OpenConnectionAsync();

        var columns = await GetColumnsAsync(context.Database.GetDbConnection(), "genres");

        columns.Should().BeEquivalentTo("id", "name");
    }

    [Fact]
    public async Task Books_author_id_has_a_foreign_key_to_authors()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.OpenConnectionAsync();

        await using var cmd = context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
            SELECT ccu.table_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
            JOIN information_schema.constraint_column_usage ccu
                ON tc.constraint_name = ccu.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
                AND tc.table_name = 'books'
                AND kcu.column_name = 'author_id'
            """;

        var referencedTable = (string?)await cmd.ExecuteScalarAsync();

        referencedTable.Should().Be("authors");
    }

    [Fact]
    public async Task Books_genre_id_has_a_foreign_key_to_genres_with_set_null_on_delete()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.OpenConnectionAsync();

        await using var cmd = context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
            SELECT rc.delete_rule
            FROM information_schema.referential_constraints rc
            JOIN information_schema.key_column_usage kcu
                ON rc.constraint_name = kcu.constraint_name
            WHERE kcu.table_name = 'books' AND kcu.column_name = 'genre_id'
            """;

        var deleteRule = (string?)await cmd.ExecuteScalarAsync();

        deleteRule.Should().Be("SET NULL");
    }

    [Fact]
    public async Task There_is_an_index_on_books_title()
    {
        await using var context = _fixture.CreateContext();
        await context.Database.OpenConnectionAsync();

        await using var cmd = context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT indexdef FROM pg_indexes WHERE tablename = 'books' AND indexname = 'ix_books_title'";

        var indexDefinition = (string?)await cmd.ExecuteScalarAsync();

        indexDefinition.Should().NotBeNull();
        indexDefinition!.Should().Contain("title");
    }

    [Fact]
    public async Task The_rating_check_constraint_rejects_out_of_range_values()
    {
        await using var context = _fixture.CreateContext();

        var author = new Models.Author { Name = "Constraint Test Author" };
        context.Authors.Add(author);
        await context.SaveChangesAsync();

        await context.Database.OpenConnectionAsync();
        await using var cmd = context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"INSERT INTO books (title, author_id, pages, rating) VALUES ('Bad Rating', {author.Id}, 100, 6)";

        var act = async () => await cmd.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task The_rating_check_constraint_allows_null_and_in_range_values()
    {
        await using var context = _fixture.CreateContext();

        var author = new Models.Author { Name = "Valid Rating Author" };
        context.Authors.Add(author);
        context.Books.Add(new Models.Book { Title = "No Rating Yet", AuthorId = author.Id, Pages = 10, Rating = null });
        context.Books.Add(new Models.Book { Title = "Five Stars", AuthorId = author.Id, Pages = 20, Rating = 5 });

        var act = async () => await context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task The_six_seeded_genres_are_present_via_HasData()
    {
        await using var context = _fixture.CreateContext();

        var genres = await context.Genres.OrderBy(g => g.Id).Select(g => g.Name).ToListAsync();

        genres.Should().Equal("Fiction", "Science Fiction", "Fantasy", "Non-Fiction", "Biography", "Mystery");
    }

    [Fact]
    public async Task The_idempotent_runtime_seed_can_run_twice_without_duplicating_rows()
    {
        await using var context = _fixture.CreateContext();

        await SeedData.EnsureDemoDataAsync(context);
        await SeedData.EnsureDemoDataAsync(context);

        var demoAuthors = await context.Authors.Where(a => a.Id == 1000).ToListAsync();
        var demoBooks = await context.Books.Where(b => b.Id == 1000).ToListAsync();

        demoAuthors.Should().ContainSingle();
        demoBooks.Should().ContainSingle();
        demoBooks.Single().Title.Should().Be("Demo Book");
    }

    [Fact]
    public async Task Renaming_page_count_to_pages_preserved_existing_data()
    {
        // Isolated from the shared fixture's normal (fully-migrated) state:
        // step the schema back to right after AddBookPageCount, insert a row
        // using the OLD column name, then step forward through the rename and
        // confirm the value survived under the new name. This is the concrete
        // proof that RenameColumn (not DropColumn+AddColumn) is what shipped.
        await using var context = _fixture.CreateContext();
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();

        try
        {
            await migrator.MigrateAsync("AddBookPageCount");

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO authors (name) VALUES ('Rename Test Author')");
            await context.Database.ExecuteSqlRawAsync("""
                INSERT INTO books (title, author_id, page_count)
                VALUES ('Rename Test Book', (SELECT id FROM authors WHERE name = 'Rename Test Author'), 321)
                """);

            await migrator.MigrateAsync(); // forward through the rename and beyond

            var pages = await context.Database.SqlQueryRaw<int>(
                "SELECT pages FROM books WHERE title = 'Rename Test Book'").SingleAsync();

            pages.Should().Be(321);
        }
        finally
        {
            // Leave the shared container fully migrated for any test that runs after this one.
            await migrator.MigrateAsync();
        }
    }
}
