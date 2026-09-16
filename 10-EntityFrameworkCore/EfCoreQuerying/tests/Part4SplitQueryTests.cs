using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Tests;

[Collection(PostgresCollection.Name)]
public class Part4SplitQueryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SingleQuery_And_SplitQuery_Return_The_Same_Logical_Data()
    {
        await using var context = fixture.CreateContext();

        var single = await context.Books
            .Include(b => b.Genres)
            .Include(b => b.Reviews)
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .Take(5)
            .ToListAsync();

        context.ChangeTracker.Clear();

        var split = await context.Books
            .Include(b => b.Genres)
            .Include(b => b.Reviews)
            .AsSplitQuery()
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .Take(5)
            .ToListAsync();

        split.Should().HaveCount(single.Count);
        for (var i = 0; i < single.Count; i++)
        {
            split[i].Id.Should().Be(single[i].Id);
            split[i].Genres.Select(g => g.Id).Should().BeEquivalentTo(single[i].Genres.Select(g => g.Id));
            split[i].Reviews.Select(r => r.Id).Should().BeEquivalentTo(single[i].Reviews.Select(r => r.Id));
        }
    }

    [Fact]
    public async Task SplitQuery_Does_Not_Duplicate_Collection_Items_For_A_Book_With_Multiple_Genres_And_Reviews()
    {
        await using var context = fixture.CreateContext();

        var bookWithBoth = await context.Books
            .Include(b => b.Genres)
            .Include(b => b.Reviews)
            .AsSplitQuery()
            .AsNoTracking()
            .Where(b => b.Genres.Count > 1 && b.Reviews.Count > 1)
            .FirstOrDefaultAsync();

        bookWithBoth.Should().NotBeNull();
        bookWithBoth!.Genres.Select(g => g.Id).Should().OnlyHaveUniqueItems();
        bookWithBoth.Reviews.Select(r => r.Id).Should().OnlyHaveUniqueItems();
    }
}
