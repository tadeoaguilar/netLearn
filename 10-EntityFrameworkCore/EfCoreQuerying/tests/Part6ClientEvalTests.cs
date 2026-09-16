using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Tests;

[Collection(PostgresCollection.Name)]
public class Part6ClientEvalTests(PostgresFixture fixture)
{
    private static bool HasRepeatedWord(string title)
    {
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length != words.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    [Fact]
    public async Task Calling_A_Local_Method_In_Where_Throws_InvalidOperationException()
    {
        await using var context = fixture.CreateContext();

        var act = async () => await context.Books.Where(b => HasRepeatedWord(b.Title)).ToListAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Rewriting_As_A_Translatable_Expression_Succeeds()
    {
        await using var context = fixture.CreateContext();

        var count = await context.Books.Where(b => b.Title.Contains("The ")).CountAsync();

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Narrowing_In_SQL_Then_Filtering_In_Memory_Is_A_Legitimate_Escape_Hatch()
    {
        await using var context = fixture.CreateContext();

        var candidates = await context.Books.Where(b => b.Price < 15).Select(b => b.Title).ToListAsync();
        var filtered = candidates.Where(HasRepeatedWord).ToList();

        filtered.Should().OnlyContain(t => HasRepeatedWord(t));
        filtered.Count.Should().BeLessThanOrEqualTo(candidates.Count);
    }
}
