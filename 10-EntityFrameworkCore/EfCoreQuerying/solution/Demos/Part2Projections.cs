using EfCoreQuerying.Dtos;
using EfCoreQuerying.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Demos;

/// <summary>
/// Part 2: Select into DTOs vs. returning full tracked entities, and
/// AsNoTracking for read-only queries.
/// </summary>
public static class Part2Projections
{
    public static async Task RunAsync(LibraryDbContext context)
    {
        Console.WriteLine("=== Part 2: Projections vs. Tracked Entities ===\n");

        // Tracked: EF Core loads full Book entities (every column) and keeps
        // a snapshot of each one so it can detect changes on SaveChanges.
        // Fine when you intend to mutate; wasteful for a read-only list page.
        var trackedBooks = await context.Books.Take(5).ToListAsync();
        Console.WriteLine($"Tracked entities loaded: {trackedBooks.Count}");
        Console.WriteLine($"Entries in the change tracker afterwards: {context.ChangeTracker.Entries().Count()}");

        context.ChangeTracker.Clear();

        // Projected: Select only the columns the DTO needs. EF Core generates
        // SQL that selects just those columns -- less data over the wire, and
        // (combined with AsNoTracking below) nothing added to the change
        // tracker, because a projection to a DTO isn't an entity anyway.
        var summaries = await context.Books
            .OrderBy(b => b.Id)
            .Take(5)
            .Select(b => new BookSummaryDto(
                b.Id,
                b.Title,
                b.Author.Name,
                b.Publisher.Name,
                b.Price,
                b.PublishedOn))
            .ToListAsync();

        Console.WriteLine($"\nProjected DTOs loaded: {summaries.Count}");
        Console.WriteLine($"Entries in the change tracker afterwards: {context.ChangeTracker.Entries().Count()} (projections are never tracked)");

        // AsNoTracking: for queries that DO return entities but are read-only
        // (e.g. rendering a page, not editing it), AsNoTracking skips the
        // snapshotting work entirely -- same entities, no tracking overhead.
        var readOnlyBooks = await context.Books
            .AsNoTracking()
            .Take(5)
            .ToListAsync();

        Console.WriteLine($"\nAsNoTracking entities loaded: {readOnlyBooks.Count}");
        Console.WriteLine($"Entries in the change tracker afterwards: {context.ChangeTracker.Entries().Count()} (AsNoTracking never adds entries)");

        Console.WriteLine();
    }
}
