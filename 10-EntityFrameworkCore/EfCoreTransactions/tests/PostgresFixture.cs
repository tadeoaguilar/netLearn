using EfCoreTransactions.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EfCoreTransactions.Tests;

/// <summary>
/// Starts one throwaway Postgres container for the whole test run (shared
/// via <see cref="PostgresCollection"/>), rather than one per test -- a
/// container takes seconds to boot, and these tests need to run many times
/// during development.
///
/// Table state, however, is NOT shared: every test class resets the
/// accounts/transfers tables in its own <c>IAsyncLifetime.InitializeAsync</c>
/// before each test method runs, so tests never see each other's rows.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("efcore_transactions_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public BankDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new BankDbContext(options);
    }

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE transfers, accounts RESTART IDENTITY CASCADE;");
    }
}

/// <summary>
/// Puts every test class that uses <see cref="PostgresFixture"/> into one
/// xUnit collection, which (a) shares a single fixture instance -- one
/// container -- across all of them, and (b) makes xUnit run them
/// sequentially rather than in parallel, since they all hit the same
/// database.
/// </summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
