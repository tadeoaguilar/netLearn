using Neo4j.Driver;

namespace GraphTransactions.Demos;

public static class Part2ExplicitReferralTransactionDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        var referrerId = $"person-{Guid.NewGuid()}";
        var newHireId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE (:Person {id: $referrerId, name: 'Referrer', referralCount: 0})", new { referrerId });
            await tx.RunAsync(
                "CREATE (:Person {id: $newHireId, name: 'New Hire', referralCount: 0})", new { newHireId });
            await tx.RunAsync(
                "CREATE (:Company {id: $companyId, name: 'Referral Target Co'})", new { companyId });
        });

        Console.WriteLine("--- Successful referral: all three writes commit together ---");
        await RunReferralAsync(session, referrerId, newHireId, companyId, simulateFailure: false);
        await PrintStateAsync(session, referrerId, newHireId, companyId);

        Console.WriteLine();
        Console.WriteLine("--- Failing referral: a deliberate throw between the 2nd and 3rd write ---");
        var secondHireId = $"person-{Guid.NewGuid()}";
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE (:Person {id: $secondHireId, name: 'Second Hire', referralCount: 0})",
                new { secondHireId });
        });

        try
        {
            await RunReferralAsync(session, referrerId, secondHireId, companyId, simulateFailure: true);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Caught: {ex.Message}");
        }
        await PrintStateAsync(session, referrerId, secondHireId, companyId);
        Console.WriteLine("Note: referralCount is unchanged AND neither the WORKS_AT nor the KNOWS");
        Console.WriteLine("relationship survived, even though both were sent to the server before the throw.");
    }

    // This is deliberately the SAME three-write shape that
    // Services/ReferralService.cs wraps -- Part 2 teaches the raw
    // BeginTransactionAsync/RunAsync/CommitAsync/RollbackAsync calls by
    // hand; Part 5 factors this exact shape into a reusable service.
    private static async Task RunReferralAsync(
        IAsyncSession session, string referrerId, string newHireId, string companyId, bool simulateFailure)
    {
        await using var tx = await session.BeginTransactionAsync();
        try
        {
            await tx.RunAsync(
                """
                MATCH (p:Person {id: $newHireId}), (c:Company {id: $companyId})
                MERGE (p)-[r:WORKS_AT]->(c)
                ON CREATE SET r.role = 'Engineer', r.since = date()
                """,
                new { newHireId, companyId });

            await tx.RunAsync(
                """
                MATCH (a:Person {id: $referrerId}), (b:Person {id: $newHireId})
                MERGE (a)-[k:KNOWS]->(b)
                ON CREATE SET k.since = date()
                """,
                new { referrerId, newHireId });

            if (simulateFailure)
            {
                throw new InvalidOperationException(
                    "Simulated failure between the KNOWS write and the referralCount increment.");
            }

            await tx.RunAsync(
                "MATCH (a:Person {id: $referrerId}) SET a.referralCount = a.referralCount + 1",
                new { referrerId });

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static async Task PrintStateAsync(
        IAsyncSession session, string referrerId, string newHireId, string companyId)
    {
        var summary = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $referrerId})
                OPTIONAL MATCH (b:Person {id: $newHireId})
                OPTIONAL MATCH (b)-[w:WORKS_AT]->(:Company {id: $companyId})
                OPTIONAL MATCH (a)-[k:KNOWS]->(b)
                RETURN a.referralCount AS referralCount, w IS NOT NULL AS worksAt, k IS NOT NULL AS knows
                """,
                new { referrerId, newHireId, companyId });
            var record = await cursor.SingleAsync();
            return (
                ReferralCount: record["referralCount"].As<int>(),
                WorksAt: record["worksAt"].As<bool>(),
                Knows: record["knows"].As<bool>());
        });

        Console.WriteLine(
            $"referrer.referralCount={summary.ReferralCount}, WORKS_AT exists={summary.WorksAt}, KNOWS exists={summary.Knows}");
    }
}
