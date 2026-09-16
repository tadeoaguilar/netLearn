using Neo4j.Driver;

namespace GraphQuerying.Queries;

/// <summary>
/// Part 4: variable-length relationships. "-[:FOLLOWS*1..3]-&gt;" finds
/// everyone reachable within 1 to 3 hops in a single query -- the same
/// question would need a recursive CTE in module 10's Postgres or an
/// application-level BFS loop (repeated round trips) in module 11's Cosmos
/// DB, because neither of those stores are optimized to walk relationships
/// as a first-class operation the way a graph database is.
/// </summary>
public static class VariableLengthDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        Console.WriteLine("\n=== Part 4: Variable-Length Relationships ===");

        await using var session = driver.AsyncSession();

        foreach (var maxHops in new[] { 1, 2, 3 })
        {
            // The hop bound in "*1..N" is part of the query's STRUCTURE, not
            // a value being compared -- Cypher does not allow parameters
            // there, only literal integers. That's fine here because maxHops
            // is a trusted value from a fixed C# array, never raw external
            // input; it is not the same risk as Part 2's string-built WHERE
            // clause, which embedded untrusted data.
            var reachable = await session.ExecuteReadAsync(async tx =>
            {
                var query =
                    $"MATCH (start:Person {{id: $id}})-[:FOLLOWS*1..{maxHops}]->(reached:Person) " +
                    "WHERE reached <> start " +
                    "RETURN count(DISTINCT reached) AS total";
                var cursor = await tx.RunAsync(query, new { id = "p0" });
                var record = await cursor.SingleAsync();
                return record["total"].As<long>();
            });

            Console.WriteLine($"People reachable from p0 within {maxHops} hop(s) via FOLLOWS: {reachable}");
        }
    }
}
