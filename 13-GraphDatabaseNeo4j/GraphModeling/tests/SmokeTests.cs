using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace GraphModeling.Tests;

public class SmokeTests : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5-community").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Can_create_and_read_back_a_node()
    {
        // GetConnectionString() returns a bolt:// URI with credentials
        // embedded, so the single-argument Driver overload is enough --
        // Testcontainers.Neo4j doesn't expose the generated password
        // separately.
        await using var driver = GraphDatabase.Driver(_container.GetConnectionString());

        await driver.VerifyConnectivityAsync();

        await using var session = driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync("CREATE (p:Person {id: $id, name: $name})",
                new { id = "p1", name = "Ada" });
            await cursor.ConsumeAsync();
        });

        var name = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:Person {id: $id}) RETURN p.name AS name", new { id = "p1" });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<string>(record["name"]);
        });

        name.Should().Be("Ada");
    }
}
