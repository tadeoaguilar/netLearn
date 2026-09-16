using EfCoreQuerying.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Demos;

/// <summary>Part 1: Where / OrderBy / Skip / Take -- filtering, sorting, paging.</summary>
public static class Part1BasicLinq
{
    public static async Task RunAsync(LibraryDbContext context)
    {
        Console.WriteLine("=== Part 1: Basic LINQ (Where / OrderBy / Skip / Take) ===\n");

        // Filter: books priced under $20, published in the last 10 years.
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10));
        var affordable = await context.Books
            .Where(b => b.Price < 20 && b.PublishedOn >= cutoff)
            .OrderBy(b => b.Price)
            .Take(5)
            .Select(b => new { b.Title, b.Price, b.PublishedOn })
            .ToListAsync();

        Console.WriteLine("Cheapest 5 recent books under $20:");
        foreach (var b in affordable)
        {
            Console.WriteLine($"  {b.Title,-30} ${b.Price,6:0.00}  {b.PublishedOn}");
        }

        // Paging: page 2 of a 10-per-page list, sorted by title.
        const int pageSize = 10;
        const int page = 2; // 1-based
        var pageTwo = await context.Books
            .OrderBy(b => b.Title)
            .ThenBy(b => b.Id) // tie-breaker: OrderBy alone isn't stable across pages without one
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => b.Title)
            .ToListAsync();

        Console.WriteLine($"\nPage {page} of the catalog (sorted by title):");
        foreach (var title in pageTwo)
        {
            Console.WriteLine($"  {title}");
        }

        Console.WriteLine();
    }
}
