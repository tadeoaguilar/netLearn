using EfCoreQuerying.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Demos;

/// <summary>
/// Part 7: raw SQL escape hatches -- FromSqlInterpolated for a query that's
/// awkward in LINQ, composed with further LINQ, and ExecuteSqlInterpolatedAsync
/// for a bulk update.
/// </summary>
public static class Part7RawSql
{
    public static async Task RunAsync(LibraryDbContext context)
    {
        Console.WriteLine("=== Part 7: Raw SQL (FromSqlInterpolated / ExecuteSqlInterpolatedAsync) ===\n");

        // "Cheapest book per author" is a top-N-per-group query: LINQ can
        // express it with a correlated subquery, but it's contorted and easy
        // to get wrong. A window function is the natural tool, and EF Core's
        // LINQ surface doesn't expose window functions directly -- this is
        // exactly the kind of query FromSqlInterpolated exists for.
        //
        // SECURITY: {} placeholders here are NOT string concatenation. EF
        // Core turns them into real ADO.NET parameters (@p0, @p1, ...) before
        // the SQL ever reaches Postgres, so user input can't break out of the
        // query. Never build this string with `$"...{value}..."` and then
        // FromSqlRaw it, and never use string.Format/+ concatenation to
        // splice values into SQL -- that reopens the exact SQL injection hole
        // interpolation here closes.
        var minPrice = 10m;
        var cheapestPerAuthor = await context.Books
            .FromSqlInterpolated($"""
                select b.* from (
                    select b.*, row_number() over (partition by b.author_id order by b.price asc) as rn
                    from books b
                ) b
                where b.rn = 1 and b.price >= {minPrice}
                """)
            // Further LINQ chains onto the raw SQL just like any other
            // IQueryable -- EF Core wraps the SQL as a subquery and composes
            // the rest normally.
            .OrderBy(b => b.Price)
            .Select(b => new { b.Title, b.Price })
            .ToListAsync();

        Console.WriteLine($"Cheapest book per author (price >= ${minPrice}), {cheapestPerAuthor.Count} authors:");
        foreach (var b in cheapestPerAuthor.Take(5))
        {
            Console.WriteLine($"  {b.Title,-30} ${b.Price:0.00}");
        }

        // Bulk update: a 10% discount on every book tagged "bestseller",
        // applied directly in the database -- no entities loaded, no
        // SaveChanges, one UPDATE statement. Same interpolation-not-
        // concatenation rule applies here: the tag value is parameterized.
        var tag = "bestseller";
        var rowsAffected = await context.Database.ExecuteSqlInterpolatedAsync($"""
            update books
            set price = round(price * 0.9, 2)
            where {tag} = any(tags)
            """);

        Console.WriteLine($"\nApplied a 10% discount to books tagged \"{tag}\": {rowsAffected} rows updated.");

        Console.WriteLine();
    }
}
