using System.Diagnostics;
using EfCoreQuerying.Persistence;
using EfCoreQuerying.Queries;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Demos;

/// <summary>Part 9: EF.CompileAsyncQuery for a hot, parameterized query path.</summary>
public static class Part9CompiledQuery
{
    public static async Task RunAsync(LibraryDbContext context)
    {
        Console.WriteLine("=== Part 9: Compiled Queries ===\n");

        var isbn = "978-0-100000-0";
        var book = await CompiledQueries.GetBookByIsbn(context, isbn);
        Console.WriteLine(book is not null
            ? $"GetBookByIsbn(\"{isbn}\") -> \"{book.Title}\""
            : $"GetBookByIsbn(\"{isbn}\") -> not found");

        var count = await CompiledQueries.CountBooksInGenre(context, "Fantasy");
        Console.WriteLine($"CountBooksInGenre(\"Fantasy\") -> {count}");

        // Illustrative timing: run the same lookup many times through the
        // compiled delegate vs. an equivalent ad-hoc LINQ query. The
        // difference is the per-call translation/cache-lookup overhead the
        // compiled query skips -- small per call, and only worth chasing
        // once a query genuinely runs often enough for that overhead to add
        // up (this loop is here to make the shape of the comparison
        // visible, not as a rigorous benchmark).
        const int iterations = 200;

        var compiledStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            await CompiledQueries.GetBookByIsbn(context, isbn);
        }
        compiledStopwatch.Stop();

        var adHocStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            await context.Books.FirstOrDefaultAsync(b => b.Isbn == isbn);
        }
        adHocStopwatch.Stop();

        Console.WriteLine($"\n{iterations} lookups -- compiled: {compiledStopwatch.ElapsedMilliseconds} ms, " +
                           $"ad-hoc LINQ: {adHocStopwatch.ElapsedMilliseconds} ms " +
                           "(both dominated by network/DB round trips here; the compiled-query saving is CPU-side " +
                           "translation overhead, which matters most under high request throughput, not in a loop like this one).");

        Console.WriteLine();
    }
}
