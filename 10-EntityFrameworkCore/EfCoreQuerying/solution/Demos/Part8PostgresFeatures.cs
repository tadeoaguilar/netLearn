using EfCoreQuerying.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Demos;

/// <summary>
/// Part 8: PostgreSQL-only querying features -- ILIKE, native array columns,
/// and JSONB.
/// </summary>
public static class Part8PostgresFeatures
{
    public static async Task RunAsync(LibraryDbContext context)
    {
        Console.WriteLine("=== Part 8: PostgreSQL-only Features ===\n");

        // EF.Functions.ILike: case-insensitive LIKE, translated directly to
        // Postgres's ILIKE operator. Plain string.Contains(...) is
        // case-sensitive in Postgres (unlike SQL Server's default collation),
        // so ILike is the idiomatic way to do a case-insensitive search here.
        var ilikeMatches = await context.Books
            .Where(b => EF.Functions.ILike(b.Title, "%the%"))
            .Select(b => b.Title)
            .ToListAsync();
        Console.WriteLine($"ILike '%the%' (case-insensitive): {ilikeMatches.Count} matches, e.g. " +
                           $"{string.Join(", ", ilikeMatches.Take(3))}");

        // Array column: Tags.Contains(...) translates to Postgres's
        // "value = ANY(tags)" -- an index-friendly membership test against
        // the native text[] column, no join table involved.
        var scifiBooks = await context.Books
            .Where(b => b.Tags.Contains("scifi"))
            .CountAsync();
        Console.WriteLine($"\nBooks tagged \"scifi\" (Tags.Contains, translates to = ANY(tags)): {scifiBooks}");

        // Overlap: "does this book have ANY of these tags" translates to
        // Postgres's array overlap operator &&.
        var wanted = new[] { "gothic", "horror-adjacent", "supernatural" };
        var overlapping = await context.Books
            .Where(b => b.Tags.Any(t => wanted.Contains(t)))
            .CountAsync();
        Console.WriteLine($"Books overlapping tags [{string.Join(", ", wanted)}]: {overlapping}");

        // JSONB: EF.Functions.JsonContains translates to the @> containment
        // operator -- "does the metadata document contain this fragment".
        var englishBooks = await context.Books
            .Where(b => EF.Functions.JsonContains(b.Metadata, """{"language":"en"}"""))
            .CountAsync();
        Console.WriteLine($"\nBooks with metadata containing {{\"language\":\"en\"}} (JsonContains -> @>): {englishBooks}");

        var featuredBooks = await context.Books
            .Where(b => EF.Functions.JsonContains(b.Metadata, """{"featured":true}"""))
            .Select(b => b.Title)
            .ToListAsync();
        Console.WriteLine($"Featured books (JsonContains): {featuredBooks.Count} -- e.g. {string.Join(", ", featuredBooks.Take(3))}");

        // Accessing a single JSON property value isn't something plain LINQ
        // over a jsonb-backed string can express (there's no CLR property to
        // point at) -- that's a job for raw SQL's ->> operator, same as any
        // other Postgres-specific expression LINQ has no vocabulary for.
        var pageCounts = await context.Books
            .FromSqlInterpolated($"select * from books where (metadata ->> 'pages')::int > {500}")
            .Select(b => b.Title)
            .ToListAsync();
        Console.WriteLine($"\nBooks with metadata->>'pages' > 500 (raw SQL ->> operator): {pageCounts.Count}");

        Console.WriteLine();
    }
}
