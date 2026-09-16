using EfCoreQuerying.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Demos;

/// <summary>
/// Part 6: the classic client-vs-server-evaluation gotcha. Some LINQ
/// expressions simply cannot be translated to SQL -- a call to an arbitrary
/// C# method is the textbook case, since the database has no idea what that
/// method does.
/// </summary>
public static class Part6ClientEval
{
    /// <summary>
    /// An ordinary C# method with no SQL equivalent. EF Core cannot translate
    /// a call to this into part of a WHERE clause -- there is no way to
    /// express "run this .NET method" as SQL.
    /// </summary>
    private static bool HasRepeatedWord(string title)
    {
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length != words.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    public static async Task RunAsync(LibraryDbContext context)
    {
        Console.WriteLine("=== Part 6: Client vs. Server Evaluation ===\n");

        Console.WriteLine("Calling a local C# method inside Where(...):");
        try
        {
            // BROKEN: EF Core tries to translate HasRepeatedWord(b.Title) into
            // SQL, fails, and -- since EF Core 3.0 -- throws instead of
            // silently pulling the whole table into memory to filter there
            // (which is what EF6 and EF Core 1.x/2.x used to do, and which
            // is far more dangerous: a query that looks selective actually
            // downloads the entire table every time).
            var broken = await context.Books
                .Where(b => HasRepeatedWord(b.Title))
                .ToListAsync();

            Console.WriteLine($"  Unexpectedly succeeded with {broken.Count} rows (should not happen for this expression).");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  Threw InvalidOperationException, as expected:");
            Console.WriteLine($"  \"{ex.Message}\"");
        }

        // FIX 1: rewrite the condition as something the provider CAN
        // translate. Here, "repeated word" was overkill anyway -- most real
        // "client eval" bugs turn out to be expressible in SQL once you stop
        // reaching for a C# helper out of habit.
        var titleHasThe = await context.Books
            .Where(b => b.Title.Contains("The "))
            .CountAsync();
        Console.WriteLine($"\n  Fix 1 (translatable rewrite): {titleHasThe} titles contain \"The \" -- runs entirely in SQL.");

        // FIX 2: when the logic genuinely cannot be expressed in SQL, filter
        // on the server as much as possible FIRST (Where/Take to shrink the
        // result set), then materialize with ToListAsync/AsEnumerable and
        // apply the client-only logic to that much smaller set explicitly.
        // This is legitimate client evaluation -- the difference from the
        // "broken" example is that it's deliberate, visible, and bounded.
        var candidates = await context.Books
            .Where(b => b.Price < 15) // narrows the set in SQL first
            .Select(b => b.Title)
            .ToListAsync();

        var repeatedWordTitles = candidates.Where(HasRepeatedWord).ToList();
        Console.WriteLine($"  Fix 2 (narrow in SQL, then filter in memory): " +
                           $"{candidates.Count} candidates -> {repeatedWordTitles.Count} with a repeated word.");

        Console.WriteLine();
    }
}
