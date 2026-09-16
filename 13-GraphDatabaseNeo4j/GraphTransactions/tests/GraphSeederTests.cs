using GraphTransactions.Data;
using Neo4j.Driver;

namespace GraphTransactions.Tests;

[Collection("Neo4j")]
public class GraphSeederTests
{
    private readonly IDriver _driver;

    public GraphSeederTests(Neo4jFixture fixture) => _driver = fixture.Driver;

    [Fact]
    public async Task SeedAsync_creates_the_expected_graph_shape_and_is_idempotent()
    {
        await GraphSeeder.SeedAsync(_driver);
        await GraphSeeder.SeedAsync(_driver); // calling it twice must not duplicate anything

        await using var session = _driver.AsyncSession();
        var personCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person) WHERE p.id IN [$aliceId, $bobId] RETURN count(p) AS count",
                new { aliceId = GraphSeeder.AliceId, bobId = GraphSeeder.BobId });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<int>(record["count"]);
        });
        personCount.Should().Be(2);

        (await GraphTestHelpers.WorksAtExistsAsync(_driver, GraphSeeder.AliceId, GraphSeeder.AcmeId)).Should().BeTrue();
        (await GraphTestHelpers.KnowsCountAsync(_driver, GraphSeeder.AliceId, GraphSeeder.BobId)).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_creates_uniqueness_constraints_on_Person_and_Company_id()
    {
        await GraphSeeder.SeedAsync(_driver);

        await using var session = _driver.AsyncSession();
        var constraintNames = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("SHOW CONSTRAINTS YIELD name RETURN name");
            var records = await cursor.ToListAsync();
            return records.Select(r => Neo4j.Driver.ValueExtensions.As<string>(r["name"])).ToList();
        });

        constraintNames.Should().Contain("person_id_unique");
        constraintNames.Should().Contain("company_id_unique");
    }
}
