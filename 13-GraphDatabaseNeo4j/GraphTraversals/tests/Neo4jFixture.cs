using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace GraphTraversals.Tests;

/// <summary>
/// Starts a single Neo4j container and seeds it once for the whole test
/// run, shared across every test class via <see cref="Neo4jCollection"/>.
/// Traversal queries are read-only, so sharing one seeded database across
/// tests is safe and much faster than spinning up a container per test
/// class.
/// </summary>
public sealed class Neo4jFixture : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5-community").Build();

    public IDriver Driver { get; private set; } = null!;

    public TraversalQueries Queries { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // GetConnectionString() returns a bolt:// URI with credentials
        // embedded, so the single-argument Driver overload is enough --
        // Testcontainers.Neo4j doesn't expose the generated password
        // separately.
        Driver = GraphDatabase.Driver(_container.GetConnectionString());
        await Driver.VerifyConnectivityAsync();

        await GraphSeeder.SeedAsync(Driver);
        Queries = new TraversalQueries(Driver);
    }

    public async Task DisposeAsync()
    {
        await Driver.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class Neo4jCollection : ICollectionFixture<Neo4jFixture>
{
    public const string Name = "Neo4j";
}
