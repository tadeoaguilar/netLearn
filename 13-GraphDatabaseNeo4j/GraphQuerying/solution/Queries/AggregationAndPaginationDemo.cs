using Neo4j.Driver;

namespace GraphQuerying.Queries;

/// <summary>
/// Part 5: aggregation (count/collect/grouping) and pagination with
/// WITH ... ORDER BY ... SKIP ... LIMIT.
/// </summary>
public static class AggregationAndPaginationDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        Console.WriteLine("\n=== Part 5: Aggregation and Pagination ===");

        await using var session = driver.AsyncSession();

        // Grouping: how many people work at each company. count(p) resets
        // per group because c.name is the only non-aggregated column being
        // returned -- Cypher groups by every non-aggregated expression
        // implicitly, unlike SQL's explicit GROUP BY.
        var perCompany = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person)-[:WORKS_AT]->(c:Company)
                RETURN c.name AS company, count(p) AS employees
                ORDER BY employees DESC
                """);
            var records = await cursor.ToListAsync();
            return records
                .Select(r => (Company: r["company"].As<string>(), Employees: r["employees"].As<long>()))
                .ToList();
        });

        Console.WriteLine("Employees per company:");
        foreach (var (company, employees) in perCompany)
        {
            Console.WriteLine($"  {company}: {employees}");
        }

        // collect(): gather every name into a single list value instead of
        // one row per match.
        var knownNames = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (:Person {id: $id})-[:KNOWS]-(other:Person) RETURN collect(other.name) AS names",
                new { id = "p0" });
            var record = await cursor.SingleAsync();
            return record["names"].As<List<string>>();
        });
        Console.WriteLine($"p0 knows: {string.Join(", ", knownNames)}");

        // Pagination: WITH re-pipes the ordered stream into SKIP/LIMIT.
        // SKIP/LIMIT must come after ORDER BY in the same WITH/RETURN
        // clause, or the page boundaries aren't stable across pages.
        const int pageSize = 20;
        var totalPeople = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:Person) RETURN count(p) AS total");
            var record = await cursor.SingleAsync();
            return record["total"].As<long>();
        });

        var totalPages = (int)Math.Ceiling(totalPeople / (double)pageSize);
        for (var page = 0; page < totalPages; page++)
        {
            var pageNames = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(
                    """
                    MATCH (p:Person)
                    WITH p
                    ORDER BY p.name
                    SKIP $skip LIMIT $limit
                    RETURN p.name AS name
                    """,
                    new { skip = page * pageSize, limit = pageSize });
                var records = await cursor.ToListAsync();
                return records.Select(r => r["name"].As<string>()).ToList();
            });

            Console.WriteLine($"Page {page} ({pageNames.Count} people): {string.Join(", ", pageNames)}");
        }
    }
}
