using Neo4j.Driver;

namespace GraphTransactions.Demos;

public static class Part1ImplicitTransactionDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        var personId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";

        // ONE ExecuteWriteAsync call, but TWO Cypher statements inside it --
        // same idea as EfCoreTransactions Part 1's "one SaveChanges(), two
        // changes": both statements succeed or both roll back, with no
        // BeginTransactionAsync anywhere in this method.
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE (:Person {id: $personId, name: 'Implicit Demo Person', referralCount: 0})",
                new { personId });
            await tx.RunAsync(
                "CREATE (:Company {id: $companyId, name: 'Implicit Demo Co'})",
                new { companyId });
        });

        var bothExist = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                OPTIONAL MATCH (p:Person {id: $personId})
                OPTIONAL MATCH (c:Company {id: $companyId})
                RETURN p IS NOT NULL AS personExists, c IS NOT NULL AS companyExists
                """,
                new { personId, companyId });
            var record = await cursor.SingleAsync();
            return record["personExists"].As<bool>() && record["companyExists"].As<bool>();
        });
        Console.WriteLine($"Both statements committed together: {bothExist}");

        // Now prove the other half: if the SECOND statement throws, the
        // FIRST statement -- already sent to the server inside this same
        // ExecuteWriteAsync call -- is rolled back too. Unlike module 10's
        // EF Core, where a second SaveChanges() call opens a brand-new
        // transaction, here both RunAsync calls are still one transaction
        // because they're inside the same ExecuteWriteAsync delegate.
        var failedPersonId = $"person-{Guid.NewGuid()}";
        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    "CREATE (:Person {id: $failedPersonId, name: 'Should Not Survive', referralCount: 0})",
                    new { failedPersonId });

                throw new InvalidOperationException("Simulated failure inside the implicit transaction.");
            });
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Caught: {ex.Message}");
        }

        var survivedCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person {id: $failedPersonId}) RETURN count(p) AS count",
                new { failedPersonId });
            var record = await cursor.SingleAsync();
            return record["count"].As<int>();
        });
        Console.WriteLine($"Person from the failed implicit transaction exists: {survivedCount > 0} (expected: False)");

        Console.WriteLine();
        Console.WriteLine("Note: this implicit transaction is NOT scoped to a single node or partition");
        Console.WriteLine("the way a Cosmos DB write is scoped to one partition key. The two CREATE");
        Console.WriteLine("statements above touched a Person AND a Company -- two different labels --");
        Console.WriteLine("inside one atomic unit, with no data-modeling changes required. Part 3 makes");
        Console.WriteLine("this contrast explicit.");
    }
}
