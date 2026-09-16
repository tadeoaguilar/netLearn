using Neo4j.Driver;

namespace GraphTransactions.Tests;

[Collection("Neo4j")]
public class ImplicitTransactionTests
{
    private readonly IDriver _driver;

    public ImplicitTransactionTests(Neo4jFixture fixture) => _driver = fixture.Driver;

    [Fact]
    public async Task ExecuteWriteAsync_with_multiple_statements_commits_all_of_them_together()
    {
        var personId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";

        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync("CREATE (:Person {id: $personId, name: 'A', referralCount: 0})", new { personId });
            await tx.RunAsync("CREATE (:Company {id: $companyId, name: 'B'})", new { companyId });
        });

        (await NodeExistsAsync("Person", personId)).Should().BeTrue();
        (await NodeExistsAsync("Company", companyId)).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteWriteAsync_rolls_back_every_statement_when_a_later_one_throws()
    {
        var personId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";

        await using var session = _driver.AsyncSession();

        var act = async () => await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync("CREATE (:Person {id: $personId, name: 'A', referralCount: 0})", new { personId });
            await tx.RunAsync("CREATE (:Company {id: $companyId, name: 'B'})", new { companyId });
            throw new InvalidOperationException("Simulated failure.");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();

        (await NodeExistsAsync("Person", personId)).Should().BeFalse(
            "the whole implicit transaction, including statements sent before the throw, must roll back");
        (await NodeExistsAsync("Company", companyId)).Should().BeFalse();
    }

    [Fact]
    public async Task A_single_transaction_can_span_a_person_and_a_company_with_no_partition_restriction()
    {
        // The Cosmos DB sibling module needed a denormalized document copied
        // into a shared partition before a similar multi-entity write could
        // be atomic (see 11-NoSqlCosmosDb/CosmosConsistencyAndTransactions).
        // Neo4j needs nothing of the kind: a Person and a Company are
        // unrelated labels, and this still commits as one unit.
        var personId = $"person-{Guid.NewGuid()}";
        var companyId = $"company-{Guid.NewGuid()}";

        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                CREATE (p:Person {id: $personId, name: 'C', referralCount: 0})
                CREATE (c:Company {id: $companyId, name: 'D'})
                CREATE (p)-[:WORKS_AT {role: 'Engineer', since: date()}]->(c)
                """,
                new { personId, companyId });
        });

        (await GraphTestHelpers.WorksAtExistsAsync(_driver, personId, companyId)).Should().BeTrue();
    }

    private async Task<bool> NodeExistsAsync(string label, string id)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $"MATCH (n:{label} {{id: $id}}) RETURN count(n) AS count", new { id });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<int>(record["count"]) > 0;
        });
    }
}
