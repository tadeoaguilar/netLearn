using EfCoreQuerying.Domain;
using EfCoreQuerying.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Queries;

/// <summary>
/// Compiled queries: EF Core normally re-translates a LINQ query (parses the
/// expression tree, builds SQL, caches the translation keyed by the
/// expression's shape) the first time it sees a given query shape, then
/// reuses that cached translation on later calls. For most code that caching
/// is already enough. <see cref="EF.CompileAsyncQuery{TContext,TParam,TResult}"/>
/// skips even the cache lookup and expression-tree walk by binding the
/// delegate once, up front -- worth it only for a query that runs very
/// often, on a hot path, where that lookup overhead is measurable (a
/// per-request lookup in a high-throughput API, not an admin report that
/// runs once a minute). The complexity cost: the query shape is now frozen
/// in a static delegate, so it can't easily be changed by conditionally
/// composed LINQ the way an ordinary method-based query can.
/// </summary>
public static class CompiledQueries
{
    /// <summary>Look up one book by ISBN -- a plausible hot path (e.g. a barcode-scan endpoint).</summary>
    public static readonly Func<LibraryDbContext, string, Task<Book?>> GetBookByIsbn =
        EF.CompileAsyncQuery((LibraryDbContext context, string isbn) =>
            context.Books.AsNoTracking().FirstOrDefault(b => b.Isbn == isbn));

    /// <summary>Count books in a genre by name -- a plausible hot path (e.g. a catalog facet count).</summary>
    public static readonly Func<LibraryDbContext, string, Task<int>> CountBooksInGenre =
        EF.CompileAsyncQuery((LibraryDbContext context, string genreName) =>
            context.Books.Count(b => b.Genres.Any(g => g.Name == genreName)));
}
