using Neo4j.Driver;

namespace GraphModeling.Tests;

/// <summary>
/// Small read helpers shared across test classes. Each one follows the
/// same rule as the reference solution: consume the cursor and project
/// into a plain value INSIDE the ExecuteReadAsync delegate, never return
/// the IResultCursor itself (it's invalid once the transaction closes).
/// </summary>
internal static class CypherAssertionHelpers
{
    public static Task<long> CountAsync(IAsyncSession session, string query, object? parameters = null)
    {
        return session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(query, parameters);
            var record = await cursor.SingleAsync();
            return ValueExtensions.As<long>(record["c"]);
        });
    }

    public static Task<List<string>> StringColumnAsync(IAsyncSession session, string query, string column)
    {
        return session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(query);
            var records = await cursor.ToListAsync();
            return records.Select(r => ValueExtensions.As<string>(r[column])).ToList();
        });
    }
}
