using GraphQuerying.Seed;
using Neo4j.Driver;

namespace GraphQuerying.Tests;

[Collection("Neo4j")]
public class DriverBasicsTests(Neo4jFixture fixture)
{
    [Fact]
    public async Task ExecuteReadAsync_counts_all_seeded_person_nodes()
    {
        await using var session = fixture.Driver.AsyncSession();

        var count = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:Person) RETURN count(p) AS total");
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<long>(record["total"]);
        });

        count.Should().Be(GraphSeeder.PersonCount);
    }

    [Fact]
    public async Task ExecuteWriteAsync_can_create_and_read_back_a_throwaway_node()
    {
        await using var session = fixture.Driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MERGE (t:TestMarker {id: $id}) SET t.value = $value",
                new { id = "driver-basics-marker", value = "hello" });
            await cursor.ConsumeAsync();
        });

        var value = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (t:TestMarker {id: $id}) RETURN t.value AS value",
                new { id = "driver-basics-marker" });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<string>(record["value"]);
        });

        value.Should().Be("hello");

        // Clean up so this test's write doesn't leak into other tests.
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (t:TestMarker {id: $id}) DELETE t",
                new { id = "driver-basics-marker" });
            await cursor.ConsumeAsync();
        });
    }
}
