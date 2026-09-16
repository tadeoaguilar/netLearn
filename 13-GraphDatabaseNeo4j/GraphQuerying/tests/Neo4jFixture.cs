using GraphQuerying.Seed;
using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace GraphQuerying.Tests;

/// <summary>
/// Starts one Neo4j container for the whole test run (not one per test
/// class), seeds it once via <see cref="GraphSeeder.SeedAsync"/> -- the same
/// seeder Program.cs uses -- and hands every test the resulting driver.
/// Reusing the real seeder means tests assert against the exact same
/// generated data the exercise's own demos run against, instead of a
/// second, possibly-drifted copy of it.
/// </summary>
public class Neo4jFixture : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5-community").Build();

    public IDriver Driver { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Driver = GraphDatabase.Driver(_container.GetConnectionString());
        await Driver.VerifyConnectivityAsync();

        await GraphSeeder.SeedAsync(Driver);
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
