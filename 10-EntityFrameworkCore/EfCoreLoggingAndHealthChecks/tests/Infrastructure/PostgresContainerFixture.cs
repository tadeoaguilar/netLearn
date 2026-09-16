using Testcontainers.PostgreSql;

namespace EfCoreLoggingAndHealthChecks.Tests.Infrastructure;

/// <summary>
/// A throwaway real PostgreSQL instance for one test class. Used via
/// <c>IClassFixture&lt;PostgresContainerFixture&gt;</c> so xUnit starts exactly one
/// container per test class (constructed once, shared by every test method in that
/// class, disposed after the class's tests finish) rather than one per test method
/// or one for the whole run.
/// </summary>
public class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("efcore_healthchecks_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
