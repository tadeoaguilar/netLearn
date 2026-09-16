using EfCoreQuerying.Persistence;
using EfCoreQuerying.Seed;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EfCoreQuerying.Tests;

/// <summary>
/// Spins up a real, throwaway PostgreSQL container for the whole test run,
/// creates the schema with EnsureCreatedAsync (this project is about
/// querying, not migrations), and seeds the deterministic library catalog
/// once. Every test asks for its own <see cref="LibraryDbContext"/> via
/// <see cref="CreateContext"/> instead of sharing one instance, since
/// DbContext isn't thread-safe and xunit only guarantees serial execution
/// within a single collection (see <see cref="PostgresCollection"/>).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("efcore_querying_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        await LibrarySeeder.SeedAsync(context);
    }

    public LibraryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new LibraryDbContext(options);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

/// <summary>
/// Every test class implements this collection so they all share one
/// container (fast) and xunit runs them serially (safe, since several tests
/// mutate rows -- see the rollback-transaction pattern in the Part 7 tests).
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
