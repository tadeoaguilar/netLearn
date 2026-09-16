using EfCoreQuerying.Queries;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Tests;

[Collection(PostgresCollection.Name)]
public class Part9CompiledQueryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Compiled_GetBookByIsbn_Returns_The_Matching_Book()
    {
        await using var context = fixture.CreateContext();

        var anyIsbn = await context.Books.Select(b => b.Isbn).FirstAsync();

        var result = await CompiledQueries.GetBookByIsbn(context, anyIsbn);

        result.Should().NotBeNull();
        result!.Isbn.Should().Be(anyIsbn);
    }

    [Fact]
    public async Task Compiled_GetBookByIsbn_Returns_Null_For_An_Unknown_Isbn()
    {
        await using var context = fixture.CreateContext();

        var result = await CompiledQueries.GetBookByIsbn(context, "does-not-exist");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Compiled_CountBooksInGenre_Matches_An_Ad_Hoc_Equivalent_Query()
    {
        await using var context = fixture.CreateContext();

        var compiledCount = await CompiledQueries.CountBooksInGenre(context, "Fantasy");
        var adHocCount = await context.Books.CountAsync(b => b.Genres.Any(g => g.Name == "Fantasy"));

        compiledCount.Should().Be(adHocCount);
        compiledCount.Should().BeGreaterThan(0);
    }
}
