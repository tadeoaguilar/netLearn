using Neo4j.Driver;

namespace GraphQuerying.Queries;

/// <summary>
/// Part 2: parameterized Cypher vs. string-built Cypher. This mirrors the
/// raw-SQL injection lesson from module 10 (Postgres) and the Cosmos SQL
/// injection lesson from module 11 -- same failure mode, different query
/// language.
/// </summary>
public static class ParameterizedQueriesDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        Console.WriteLine("\n=== Part 2: Parameterized Queries ===");

        await using var session = driver.AsyncSession();

        // Shaped like a Cypher-injection payload. It is NOT a real person
        // name in the seed data.
        var maliciousInput = "Nobody\" OR 1=1 //";

        // NEVER do this -- string-built Cypher is a Cypher injection hole
        // exactly like string-built SQL or hand-built Cosmos SQL. Shown here
        // ONLY to prove the point; never ship code that builds a query
        // string out of external input.
        var unsafeQuery = $"MATCH (p:Person) WHERE p.name = \"{maliciousInput}\" RETURN p.name AS name";
        var unsafeMatchCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(unsafeQuery);
            var records = await cursor.ToListAsync();
            return records.Count;
        });

        Console.WriteLine(
            $"[UNSAFE] String-built query matched {unsafeMatchCount} people " +
            "(the 'OR 1=1' escaped the intended filter and matched everyone).");

        // The safe version: the exact same value, passed as a PARAMETER.
        // Neo4j treats $name as an opaque string to compare p.name against
        // -- it can never restructure the query, no matter what it contains.
        var safeMatchCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person) WHERE p.name = $name RETURN p.name AS name",
                new { name = maliciousInput });
            var records = await cursor.ToListAsync();
            return records.Count;
        });

        Console.WriteLine(
            $"[SAFE] Parameterized query matched {safeMatchCount} people " +
            "(the payload is inert -- just a string nobody happens to be named).");
    }
}
