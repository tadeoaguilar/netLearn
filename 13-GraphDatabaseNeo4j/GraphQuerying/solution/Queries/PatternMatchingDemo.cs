using Neo4j.Driver;

namespace GraphQuerying.Queries;

/// <summary>
/// Part 3: pattern matching -- filtering on node properties, filtering on
/// relationship properties, matching more than one relationship type in a
/// single pattern, and the inline-property-vs-WHERE choice.
/// </summary>
public static class PatternMatchingDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        Console.WriteLine("\n=== Part 3: Pattern Matching ===");

        await using var session = driver.AsyncSession();

        // Inline pattern property ({id: $id}): reads best when you're
        // pinning down exactly which node to start from -- the filter is
        // part of the shape you're matching, not a separate condition.
        var byInlineId = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person {id: $id}) RETURN p.name AS name",
                new { id = "p0" });
            var record = await cursor.SingleAsync();
            return record["name"].As<string>();
        });
        Console.WriteLine($"p0 (inline match) is named: {byInlineId}");

        // WHERE clause: reads best for comparisons, multiple conditions, or
        // anything beyond simple equality -- squeezing a range comparison
        // into "{...}" inline syntax isn't even possible.
        var recentHiresAtC0 = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person)-[r:WORKS_AT]->(c:Company {id: $companyId})
                WHERE r.since >= $sinceYear
                RETURN count(p) AS total
                """,
                new { companyId = "c0", sinceYear = 2020 });
            var record = await cursor.SingleAsync();
            return record["total"].As<long>();
        });
        Console.WriteLine($"People who joined Initech (c0) since 2020: {recentHiresAtC0}");

        // Filtering by a relationship property.
        var recentKnowsCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (:Person)-[r:KNOWS]-(:Person) WHERE r.since > $year RETURN count(r) AS total",
                new { year = 2020 });
            var record = await cursor.SingleAsync();
            // Every undirected KNOWS edge is traversed from both ends, so
            // the raw count is double the edge count -- halve it back out.
            return record["total"].As<long>() / 2;
        });
        Console.WriteLine($"KNOWS relationships formed after 2020: {recentKnowsCount}");

        // Multiple relationship types in one pattern: "|" between relationship
        // types in a single MATCH, instead of two separate MATCH clauses
        // unioned together.
        var connectionCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (:Person {id: $id})-[:KNOWS|FOLLOWS]-(other:Person) RETURN count(DISTINCT other) AS total",
                new { id = "p0" });
            var record = await cursor.SingleAsync();
            return record["total"].As<long>();
        });
        Console.WriteLine($"People p0 either knows or follows/is followed by: {connectionCount}");
    }
}
