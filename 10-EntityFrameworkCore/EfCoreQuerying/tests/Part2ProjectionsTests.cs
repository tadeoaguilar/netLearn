using EfCoreQuerying.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Tests;

[Collection(PostgresCollection.Name)]
public class Part2ProjectionsTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Select_Projection_Populates_Dto_Fields_From_Related_Entities()
    {
        await using var context = fixture.CreateContext();

        var summary = await context.Books
            .OrderBy(b => b.Id)
            .Select(b => new BookSummaryDto(b.Id, b.Title, b.Author.Name, b.Publisher.Name, b.Price, b.PublishedOn))
            .FirstAsync();

        summary.Title.Should().NotBeNullOrWhiteSpace();
        summary.AuthorName.Should().NotBeNullOrWhiteSpace();
        summary.PublisherName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Select_Projection_Adds_Nothing_To_The_Change_Tracker()
    {
        await using var context = fixture.CreateContext();

        await context.Books
            .Take(5)
            .Select(b => new BookSummaryDto(b.Id, b.Title, b.Author.Name, b.Publisher.Name, b.Price, b.PublishedOn))
            .ToListAsync();

        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task AsNoTracking_Loads_Entities_Without_Tracking_Them()
    {
        await using var context = fixture.CreateContext();

        var tracked = await context.Books.Take(3).ToListAsync();
        context.ChangeTracker.Entries().Should().HaveCount(3);

        context.ChangeTracker.Clear();

        var untracked = await context.Books.AsNoTracking().Take(3).ToListAsync();
        untracked.Should().HaveCount(3);
        context.ChangeTracker.Entries().Should().BeEmpty();
    }
}
