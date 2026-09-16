using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace GraphTransactions.Tests;

// Shared across the whole "Neo4j" test collection -- one container, one
// driver, started once. Individual tests use uniquely-generated (Guid-based)
// ids so they stay independent of each other regardless of execution order.
public class Neo4jFixture : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5-community").Build();

    public IDriver Driver { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // GetConnectionString() returns a bolt:// URI with credentials
        // embedded, so the single-argument Driver overload is enough --
        // Testcontainers.Neo4j doesn't expose the generated password
        // separately.
        Driver = GraphDatabase.Driver(_container.GetConnectionString());
        await Driver.VerifyConnectivityAsync();

        await using var session = Driver.AsyncSession();
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
    }

    public async Task DisposeAsync()
    {
        await Driver.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Neo4j")]
public class Neo4jCollection : ICollectionFixture<Neo4jFixture>
{
}
