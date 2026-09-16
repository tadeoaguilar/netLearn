using EfCoreQuerying.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Tests;

[Collection(PostgresCollection.Name)]
public class Part5GroupByTests(PostgresFixture fixture)
{
    [Fact]
    public async Task GroupBy_Reviews_Computes_Correct_Average_Rating_Per_Book()
    {
        await using var context = fixture.CreateContext();

        var ratings = await context.Reviews
            .GroupBy(r => r.BookId)
            .Select(g => new BookRatingDto(g.Key, g.First().Book.Title, g.Average(r => (double)r.Rating), g.Count()))
            .ToListAsync();

        ratings.Should().NotBeEmpty();

        // Cross-check one book's aggregate against a manual calculation.
        var sample = ratings.First();
        var manualReviews = await context.Reviews
            .Where(r => r.BookId == sample.BookId)
            .Select(r => r.Rating)
            .ToListAsync();

        sample.ReviewCount.Should().Be(manualReviews.Count);
        sample.AverageRating.Should().BeApproximately(manualReviews.Average(), 0.0001);
    }

    [Fact]
    public async Task GroupBy_Genre_Book_Counts_Sum_To_Total_Book_Genre_Links()
    {
        await using var context = fixture.CreateContext();

        var perGenre = await context.Genres
            .Select(g => new GenreBookCountDto(g.Name, g.Books.Count))
            .ToListAsync();

        var totalLinks = perGenre.Sum(g => g.BookCount);
        var expectedLinks = await context.Books.SelectMany(b => b.Genres).CountAsync();

        totalLinks.Should().Be(expectedLinks);
    }

    [Fact]
    public async Task GroupBy_Reviews_OrderByDescending_Returns_Highest_Rated_Book_First()
    {
        await using var context = fixture.CreateContext();

        var top = await context.Reviews
            .GroupBy(r => r.BookId)
            .Select(g => new BookRatingDto(g.Key, g.First().Book.Title, g.Average(r => (double)r.Rating), g.Count()))
            .OrderByDescending(r => r.AverageRating)
            .ToListAsync();

        top.Should().NotBeEmpty();
        top.Should().BeInDescendingOrder(r => r.AverageRating);
    }
}
