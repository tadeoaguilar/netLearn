using Neo4j.Driver;

namespace GraphTransactions.Services;

public class ReferralService : IReferralService
{
    private readonly IDriver _driver;

    public ReferralService(IDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    public async Task<ReferralResult> ReferAsync(
        string referrerId,
        string newHireId,
        string companyId,
        string role,
        bool simulateFailureBeforeIncrement = false)
    {
        await using var session = _driver.AsyncSession();
        await using var tx = await session.BeginTransactionAsync();
        try
        {
            // Write 1: the new hire starts working at the company. MERGE so
            // calling this twice for the same pair updates the role instead
            // of creating a second WORKS_AT relationship.
            await tx.RunAsync(
                """
                MATCH (p:Person {id: $newHireId}), (c:Company {id: $companyId})
                MERGE (p)-[r:WORKS_AT]->(c)
                ON CREATE SET r.role = $role, r.since = date()
                ON MATCH SET r.role = $role
                """,
                new { newHireId, companyId, role });

            // Write 2: the referrer now KNOWS the new hire -- MERGE so an
            // existing relationship (they may already know each other) is
            // reused rather than duplicated.
            await tx.RunAsync(
                """
                MATCH (a:Person {id: $referrerId}), (b:Person {id: $newHireId})
                MERGE (a)-[k:KNOWS]->(b)
                ON CREATE SET k.since = date()
                """,
                new { referrerId, newHireId });

            if (simulateFailureBeforeIncrement)
            {
                // Deliberate failure between the second and third write.
                // EXERCISE.md Part 2 and the test suite use this flag to
                // prove the whole transaction -- including the two writes
                // ABOVE this line, already sent to the server -- rolls back,
                // not just the write that never ran.
                throw new InvalidOperationException(
                    "Simulated failure between the KNOWS write and the referralCount increment.");
            }

            // Write 3: credit the referrer.
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $referrerId})
                SET a.referralCount = a.referralCount + 1
                RETURN a.referralCount AS referralCount
                """,
                new { referrerId });
            var record = await cursor.SingleAsync();
            var referralCount = record["referralCount"].As<int>();

            await tx.CommitAsync();
            return new ReferralResult(referrerId, newHireId, companyId, referralCount);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
