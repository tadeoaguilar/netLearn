using Neo4j.Driver;

namespace GraphTraversals.Tests;

[Collection(Neo4jCollection.Name)]
public class SeedTests
{
    private readonly Neo4jFixture _fixture;

    public SeedTests(Neo4jFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_people()
    {
        // GraphSeeder is MERGE-based, so re-seeding an already-seeded
        // database must be a no-op rather than doubling up nodes.
        await GraphSeeder.SeedAsync(_fixture.Driver);

        await using var session = _fixture.Driver.AsyncSession();
        var personCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:Person) RETURN count(p) AS count");
            var record = await cursor.SingleAsync();
            // Both FluentAssertions and Neo4j.Driver define an As<T>()
            // extension in this scope, so the call is ambiguous unless
            // qualified explicitly.
            return Neo4j.Driver.ValueExtensions.As<long>(record["count"]);
        });

        personCount.Should().Be(GraphSeeder.People.Count);
    }

    [Fact]
    public async Task Alice_and_bob_each_have_five_knows_relationships()
    {
        // Alice and Bob are the designed hubs of the sample graph.
        var aliceNeighbors = await _fixture.Queries.WithinHopsAsync("alice", maxHops: 1);
        var bobNeighbors = await _fixture.Queries.WithinHopsAsync("bob", maxHops: 1);

        aliceNeighbors.Should().HaveCount(5);
        bobNeighbors.Should().HaveCount(5);
    }
}
