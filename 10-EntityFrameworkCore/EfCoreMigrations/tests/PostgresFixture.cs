using EfCoreMigrations.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EfCoreMigrations.Tests;

/// <summary>
/// Starts one throwaway Postgres container for the whole test class, applies
/// the full migration history to it once, and hands out fresh
/// <see cref="AppDbContext"/> instances pointed at it.
///
/// Requires Docker. There is no in-memory or SQLite fallback here on purpose
/// -- the whole point of this project is that migrations behave correctly
/// against real PostgreSQL (check constraints, identity columns, the
/// snake_case naming convention), none of which a stand-in provider proves.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("efcore_migrations_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }
}
