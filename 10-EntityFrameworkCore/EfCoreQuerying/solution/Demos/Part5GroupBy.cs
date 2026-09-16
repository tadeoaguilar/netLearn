using EfCoreQuerying.Dtos;
using EfCoreQuerying.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Demos;

/// <summary>Part 5: GroupBy and aggregates -- average rating per book, book count per genre.</summary>
public static class Part5GroupBy
{
    public static async Task RunAsync(LibraryDbContext context)
    {
        Console.WriteLine("=== Part 5: GroupBy and Aggregates ===\n");

        // Average rating per book -- group Reviews by BookId, then join back
        // to Books for the title. EF Core translates this to a SQL GROUP BY
        // with AVG()/COUNT(), computed entirely in the database.
        var ratings = await context.Reviews
            .GroupBy(r => r.BookId)
            .Select(g => new BookRatingDto(
                g.Key,
                g.First().Book.Title,
                g.Average(r => (double)r.Rating),
                g.Count()))
            .OrderByDescending(r => r.AverageRating)
            .Take(5)
            .ToListAsync();

        Console.WriteLine("Top 5 books by average rating (books with at least one review):");
        foreach (var r in ratings)
        {
            Console.WriteLine($"  {r.Title,-30} avg {r.AverageRating:0.00} ({r.ReviewCount} reviews)");
        }

        // Book count per genre -- group across the many-to-many navigation.
        var perGenre = await context.Genres
            .Select(g => new GenreBookCountDto(g.Name, g.Books.Count))
            .OrderByDescending(g => g.BookCount)
            .ToListAsync();

        Console.WriteLine("\nBooks per genre:");
        foreach (var g in perGenre)
        {
            Console.WriteLine($"  {g.GenreName,-16} {g.BookCount}");
        }

        Console.WriteLine();
    }
}
