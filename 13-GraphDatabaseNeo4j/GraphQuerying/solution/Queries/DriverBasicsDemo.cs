using Neo4j.Driver;

namespace GraphQuerying.Queries;

/// <summary>
/// Part 1: the driver's read/write split. <see cref="IAsyncSession.ExecuteReadAsync{T}"/>
/// and <see cref="IAsyncSession.ExecuteWriteAsync{T}"/> aren't just naming
/// conventions -- against a Neo4j *cluster*, the driver uses them to route
/// reads to any available replica and writes to the current leader. Against
/// this exercise's single-instance container there's no visible difference,
/// but calling the wrong one in real code either fails outright (a write
/// inside ExecuteReadAsync) or defeats read-scaling (a read inside
/// ExecuteWriteAsync), so the habit matters even here.
/// </summary>
public static class DriverBasicsDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        Console.WriteLine("\n=== Part 1: Driver Basics (IDriver / IAsyncSession) ===");

        await using var session = driver.AsyncSession();

        var personCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:Person) RETURN count(p) AS total");
            var record = await cursor.SingleAsync();
            return record["total"].As<long>();
        });

        var companyCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (c:Company) RETURN count(c) AS total");
            var record = await cursor.SingleAsync();
            return record["total"].As<long>();
        });

        Console.WriteLine($"Total Person nodes: {personCount}");
        Console.WriteLine($"Total Company nodes: {companyCount}");
    }
}
