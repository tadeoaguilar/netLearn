using Neo4j.Driver;

namespace GraphTransactions.Data;

// Idempotent: safe to call every time the app starts. The constraints use
// IF NOT EXISTS, and the seed data uses MERGE keyed on a stable business
// id (not the auto-generated Guid default on Domain.Person/Company), so
// re-running this never creates duplicate nodes or relationships.
public static class GraphSeeder
{
    public const string AliceId = "person-alice";
    public const string BobId = "person-bob";
    public const string AcmeId = "company-acme";

    public static async Task SeedAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE CONSTRAINT person_id_unique IF NOT EXISTS FOR (p:Person) REQUIRE p.id IS UNIQUE");
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE CONSTRAINT company_id_unique IF NOT EXISTS FOR (c:Company) REQUIRE c.id IS UNIQUE");
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                MERGE (alice:Person {id: $aliceId})
                ON CREATE SET alice.name = 'Alice', alice.referralCount = 0
                MERGE (bob:Person {id: $bobId})
                ON CREATE SET bob.name = 'Bob', bob.referralCount = 0
                MERGE (acme:Company {id: $acmeId})
                ON CREATE SET acme.name = 'Acme Corp'
                MERGE (alice)-[:WORKS_AT {role: 'Engineering Manager', since: date('2019-03-01')}]->(acme)
                MERGE (alice)-[:KNOWS {since: date('2018-05-12')}]->(bob)
                """,
                new { aliceId = AliceId, bobId = BobId, acmeId = AcmeId });
        });
    }
}
