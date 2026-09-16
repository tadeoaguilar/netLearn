using EfCoreQuerying.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Demos;

/// <summary>
/// Part 4: AsSplitQuery vs. the default single query with multiple Includes
/// -- the cartesian-explosion problem.
/// </summary>
public static class Part4SplitQuery
{
    public static async Task RunAsync(LibraryDbContext context)
    {
        Console.WriteLine("=== Part 4: AsSplitQuery vs. Single Query ===\n");

        // Default behaviour: EF Core joins Books to Genres (many-to-many) AND
        // Reviews (one-to-many) in a SINGLE SQL query. Each Book row gets
        // duplicated once per (genre, review) combination in the join result
        // -- a book with 2 genres and 3 reviews comes back as 6 duplicated
        // rows that EF Core has to de-duplicate client-side. With more
        // collection Includes, or bigger collections, this "cartesian
        // explosion" means far more data crosses the wire than the row count
        // suggests.
        var singleQuery = await context.Books
            .Include(b => b.Genres)
            .Include(b => b.Reviews)
            .AsNoTracking()
            .Where(b => b.Id <= 5)
            .ToListAsync();

        Console.WriteLine("Single query (default): one round trip, but the underlying result set is " +
                           "genres x reviews per book before EF Core folds it back together.");
        foreach (var b in singleQuery)
        {
            Console.WriteLine($"  {b.Title}: {b.Genres.Count} genres, {b.Reviews.Count} reviews");
        }

        // AsSplitQuery: EF Core issues one SQL query per collection
        // navigation (one for Books+Authors/Publishers, one for the Genres
        // join, one for Reviews), avoiding the duplication -- at the cost of
        // multiple round trips instead of one, and losing the single-query
        // consistency guarantee within a transaction (rows across the split
        // queries could reflect concurrent writes between round trips, unless
        // wrapped in an explicit transaction).
        var splitQuery = await context.Books
            .Include(b => b.Genres)
            .Include(b => b.Reviews)
            .AsSplitQuery()
            .AsNoTracking()
            .Where(b => b.Id <= 5)
            .ToListAsync();

        Console.WriteLine("\nAsSplitQuery: multiple round trips, no row duplication in any single result set.");
        foreach (var b in splitQuery)
        {
            Console.WriteLine($"  {b.Title}: {b.Genres.Count} genres, {b.Reviews.Count} reviews");
        }

        Console.WriteLine("\nWhen to split: multiple collection Includes on the same root, where the " +
                           "collections are large or numerous enough that duplication would move a lot of " +
                           "redundant data. When NOT to split: a single collection Include (no duplication " +
                           "possible), small/bounded collections, or when you need one consistent snapshot " +
                           "across all the data in a single round trip.");
        Console.WriteLine();
    }
}
