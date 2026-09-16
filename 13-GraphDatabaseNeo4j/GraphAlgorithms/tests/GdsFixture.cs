using GraphAlgorithms.Seeding;
using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace GraphAlgorithms.Tests;

/// <summary>
/// Shared Testcontainers fixture for the GDS-backed integration tests.
/// Unlike the other four projects in this module, this container needs
/// the Graph Data Science plugin loaded (NEO4J_PLUGINS below), which makes
/// it noticeably slower to become ready than a plain `neo4j:5-community`
/// container -- hence the generous 5-minute startup budget instead of
/// Testcontainers' default. The container and seed data are shared across
/// every test in GdsAlgorithmTests so we only pay that startup cost once
/// per test run.
/// </summary>
public sealed class GdsFixture : IAsyncLifetime
{
    private readonly Neo4jContainer _container = new Neo4jBuilder("neo4j:5-community")
        .WithEnvironment("NEO4J_PLUGINS", "[\"graph-data-science\"]")
        .Build();

    public IDriver Driver { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        using var startupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await _container.StartAsync(startupTimeout.Token);

        // GetConnectionString() returns a bolt:// URI with credentials
        // embedded, so the single-argument Driver overload is enough --
        // Testcontainers.Neo4j doesn't expose the generated password
        // separately.
        Driver = GraphDatabase.Driver(_container.GetConnectionString());
        await Driver.VerifyConnectivityAsync();

        await using var session = Driver.AsyncSession();
        await GraphSeeder.SeedAsync(session);
    }

    public async Task DisposeAsync()
    {
        await Driver.DisposeAsync();
        await _container.DisposeAsync();
    }
}
