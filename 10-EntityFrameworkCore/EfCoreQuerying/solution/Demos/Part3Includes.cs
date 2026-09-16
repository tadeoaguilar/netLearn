using EfCoreQuerying.Dtos;
using EfCoreQuerying.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Demos;

/// <summary>
/// Part 3: Include/ThenInclude for eager loading vs. projecting related data
/// directly with Select.
/// </summary>
public static class Part3Includes
{
    public static async Task RunAsync(LibraryDbContext context)
    {
        Console.WriteLine("=== Part 3: Include/ThenInclude vs. Projection ===\n");

        // Include/ThenInclude: loads full Author, Publisher, Genres and
        // Reviews entities alongside each Book, as tracked navigation
        // properties. Necessary when you need to MUTATE the related data
        // (e.g. adding a Review to a Book you already loaded), or you
        // genuinely need every column of every related entity.
        var withIncludes = await context.Books
            .Include(b => b.Author)
            .Include(b => b.Publisher)
            .Include(b => b.Genres)
            .Include(b => b.Reviews)
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .Take(3)
            .ToListAsync();

        Console.WriteLine("Via Include/ThenInclude:");
        foreach (var book in withIncludes)
        {
            var avg = book.Reviews.Count > 0 ? book.Reviews.Average(r => r.Rating) : (double?)null;
            Console.WriteLine($"  {book.Title} by {book.Author.Name} ({book.Publisher.Name}) -- " +
                               $"genres: {string.Join(", ", book.Genres.Select(g => g.Name))}, avg rating: {avg?.ToString("0.0") ?? "n/a"}");
        }

        // Projection: when the caller only needs a handful of fields from the
        // related entities (not the whole related entity, not the ability to
        // mutate it), Select is strictly better than Include -- EF Core
        // generates one query that selects exactly those columns, with no
        // extra tracked graph left behind. This is the recommended default
        // for read-only view models.
        var projected = await context.Books
            .OrderBy(b => b.Id)
            .Take(3)
            .Select(b => new BookDetailDto(
                b.Id,
                b.Title,
                b.Author.Name,
                b.Publisher.Name,
                b.Genres.Select(g => g.Name).ToList(),
                b.Reviews.Select(r => r.Comment ?? "(no comment)").ToList(),
                b.Reviews.Count > 0 ? b.Reviews.Average(r => (double)r.Rating) : (double?)null))
            .ToListAsync();

        Console.WriteLine("\nVia projection (Select):");
        foreach (var dto in projected)
        {
            Console.WriteLine($"  {dto.Title} by {dto.AuthorName} ({dto.PublisherName}) -- " +
                               $"genres: {string.Join(", ", dto.GenreNames)}, avg rating: {dto.AverageRating?.ToString("0.0") ?? "n/a"}");
        }

        Console.WriteLine("\nWhen projection is strictly better: read-only views, list/detail pages, " +
                           "API responses -- anywhere you don't need to track or mutate the related entities. " +
                           "Include still earns its keep when you're about to add/update/remove related rows " +
                           "through the same context.");
        Console.WriteLine();
    }
}
