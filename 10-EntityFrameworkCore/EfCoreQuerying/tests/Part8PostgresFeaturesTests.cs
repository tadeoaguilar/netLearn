using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Tests;

[Collection(PostgresCollection.Name)]
public class Part8PostgresFeaturesTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ILike_Matches_Case_Insensitively()
    {
        await using var context = fixture.CreateContext();

        var lower = await context.Books.Where(b => EF.Functions.ILike(b.Title, "%the%")).CountAsync();
        var upper = await context.Books.Where(b => EF.Functions.ILike(b.Title, "%THE%")).CountAsync();
        var mixed = await context.Books.Where(b => EF.Functions.ILike(b.Title, "%ThE%")).CountAsync();

        lower.Should().BeGreaterThan(0);
        lower.Should().Be(upper);
        lower.Should().Be(mixed);
    }

    [Fact]
    public async Task Case_Sensitive_Like_Style_Contains_Differs_From_ILike()
    {
        await using var context = fixture.CreateContext();

        // Postgres text comparison is case-sensitive by default, so a
        // deliberately wrong-case needle should never match via plain
        // Contains, while ILike (case-insensitive) still finds it.
        var caseSensitive = await context.Books.Where(b => b.Title.Contains("tHE")).CountAsync();
        var caseInsensitive = await context.Books.Where(b => EF.Functions.ILike(b.Title, "%tHE%")).CountAsync();

        caseSensitive.Should().Be(0);
        caseInsensitive.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Tags_Contains_Translates_To_Array_Membership_Test()
    {
        await using var context = fixture.CreateContext();

        var scifiBooks = await context.Books.Where(b => b.Tags.Contains("scifi")).ToListAsync();

        scifiBooks.Should().NotBeEmpty();
        scifiBooks.Should().OnlyContain(b => b.Tags.Contains("scifi"));
    }

    [Fact]
    public async Task Tags_Any_With_Contains_Translates_To_Array_Overlap()
    {
        await using var context = fixture.CreateContext();

        var wanted = new[] { "gothic", "horror" };
        var overlapping = await context.Books.Where(b => b.Tags.Any(t => wanted.Contains(t))).ToListAsync();

        overlapping.Should().OnlyContain(b => b.Tags.Any(t => wanted.Contains(t)));
    }

    [Fact]
    public async Task JsonContains_Filters_By_Top_Level_Property_Value()
    {
        await using var context = fixture.CreateContext();

        var englishBooks = await context.Books
            .Where(b => EF.Functions.JsonContains(b.Metadata, """{"language":"en"}"""))
            .ToListAsync();

        englishBooks.Should().NotBeEmpty();
        englishBooks.Should().OnlyContain(b => b.Metadata.Contains("\"language\":\"en\""));
    }

    [Fact]
    public async Task JsonContains_Filters_By_Boolean_Flag()
    {
        await using var context = fixture.CreateContext();

        var featured = await context.Books
            .Where(b => EF.Functions.JsonContains(b.Metadata, """{"featured":true}"""))
            .CountAsync();
        var notFeatured = await context.Books
            .Where(b => EF.Functions.JsonContains(b.Metadata, """{"featured":false}"""))
            .CountAsync();
        var total = await context.Books.CountAsync();

        (featured + notFeatured).Should().Be(total);
    }

    [Fact]
    public async Task Raw_Sql_Extracts_A_Scalar_Json_Property_With_The_Arrow_Operator()
    {
        await using var context = fixture.CreateContext();

        var longBooks = await context.Books
            .FromSqlInterpolated($"select * from books where (metadata ->> 'pages')::int > {500}")
            .ToListAsync();

        longBooks.Should().OnlyContain(b => b.Metadata.Contains("\"pages\""));
    }
}
