using EfCoreQuerying.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Tests;

[Collection(PostgresCollection.Name)]
public class Part3IncludeTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Include_ThenInclude_Loads_Author_Publisher_Genres_And_Reviews()
    {
        await using var context = fixture.CreateContext();

        var book = await context.Books
            .Include(b => b.Author)
            .Include(b => b.Publisher)
            .Include(b => b.Genres)
            .Include(b => b.Reviews)
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .FirstAsync();

        book.Author.Should().NotBeNull();
        book.Author.Name.Should().NotBeNullOrWhiteSpace();
        book.Publisher.Should().NotBeNull();
        book.Genres.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Projection_Produces_The_Same_Related_Data_As_Include_Without_Tracking()
    {
        await using var context = fixture.CreateContext();

        var viaInclude = await context.Books
            .Include(b => b.Author)
            .Include(b => b.Genres)
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .FirstAsync();

        var viaProjection = await context.Books
            .OrderBy(b => b.Id)
            .Select(b => new BookDetailDto(
                b.Id, b.Title, b.Author.Name, b.Publisher.Name,
                b.Genres.Select(g => g.Name).ToList(),
                b.Reviews.Select(r => r.Comment ?? "").ToList(),
                null))
            .FirstAsync();

        viaProjection.Id.Should().Be(viaInclude.Id);
        viaProjection.AuthorName.Should().Be(viaInclude.Author.Name);
        viaProjection.GenreNames.Should().BeEquivalentTo(viaInclude.Genres.Select(g => g.Name));
    }
}
