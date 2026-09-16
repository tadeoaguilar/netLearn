using GraphModeling.Persistence;
using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace GraphModeling.Tests;

/// <summary>
/// One real Neo4j container, started once and shared across every test
/// class in this project via the "Neo4j graph" collection below. A fresh
/// container per test class (the pattern <c>SmokeTests</c> uses) would
/// work too, but Testcontainers' per-container startup cost (image pull
/// check, container boot, bolt port ready) is easily a few seconds each
/// time -- worth paying once for a project with this many test classes.
///
/// The schema (constraints/indexes) and the shared seed dataset are
/// applied once here, in <see cref="InitializeAsync"/>, so every test
/// class can assume both already exist without re-seeding itself.
/// </summary>
public class Neo4jSharedFixture : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5-community").Build();

    public IDriver Driver { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // GetConnectionString() returns a bolt:// URI with credentials
        // already embedded, so the single-argument Driver overload is
        // enough -- Testcontainers.Neo4j doesn't expose the generated
        // password separately.
        Driver = GraphDatabase.Driver(_container.GetConnectionString());
        await Driver.VerifyConnectivityAsync();

        await using var session = Driver.AsyncSession();
        await GraphSchema.EnsureConstraintsAndIndexesAsync(session);
        await GraphSeeder.SeedAsync(session);
    }

    public async Task DisposeAsync()
    {
        await Driver.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Neo4j graph")]
public class Neo4jGraphCollection : ICollectionFixture<Neo4jSharedFixture>
{
    // No code needed -- this class only exists to attach
    // ICollectionFixture<Neo4jSharedFixture> to the "Neo4j graph" name
    // that [Collection("Neo4j graph")] refers to on each test class.
}
