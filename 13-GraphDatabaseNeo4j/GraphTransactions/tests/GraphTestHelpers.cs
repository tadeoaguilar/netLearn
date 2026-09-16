using Neo4j.Driver;

namespace GraphTransactions.Tests;

// FluentAssertions and Neo4j.Driver are both in scope in this test project,
// and both define an extension method named As<T>() -- so every call here
// uses the fully-qualified Neo4j.Driver.ValueExtensions.As<T>(...) form
// instead of the ambiguous record["x"].As<T>() shorthand the solution
// project can use safely (it never references FluentAssertions).
internal static class GraphTestHelpers
{
    public static async Task CreatePersonAsync(IDriver driver, string id, string name, int referralCount = 0)
    {
        await using var session = driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE (:Person {id: $id, name: $name, referralCount: $referralCount})",
                new { id, name, referralCount });
        });
    }

    public static async Task CreateCompanyAsync(IDriver driver, string id, string name)
    {
        await using var session = driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync("CREATE (:Company {id: $id, name: $name})", new { id, name });
        });
    }

    public static async Task<int> GetReferralCountAsync(IDriver driver, string personId)
    {
        await using var session = driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person {id: $personId}) RETURN p.referralCount AS referralCount",
                new { personId });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<int>(record["referralCount"]);
        });
    }

    public static async Task<bool> WorksAtExistsAsync(IDriver driver, string personId, string companyId)
    {
        await using var session = driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person {id: $personId})-[r:WORKS_AT]->(c:Company {id: $companyId})
                RETURN count(r) AS count
                """,
                new { personId, companyId });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<int>(record["count"]) > 0;
        });
    }

    public static async Task<int> KnowsCountAsync(IDriver driver, string fromId, string toId)
    {
        await using var session = driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $fromId})-[r:KNOWS]->(b:Person {id: $toId})
                RETURN count(r) AS count
                """,
                new { fromId, toId });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<int>(record["count"]);
        });
    }
}
