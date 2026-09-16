using EfCoreLoggingAndHealthChecks.Data;
using EfCoreLoggingAndHealthChecks.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Testcontainers.PostgreSql;

namespace EfCoreLoggingAndHealthChecks.Tests;

/// <summary>
/// Each test method here needs to control, by itself, whether the migration has
/// been applied -- so unlike the other test classes this one does NOT share a
/// container via <c>IClassFixture</c> (that would let one test's Migrate() call
/// leak into another test's "nothing applied yet" assertion depending on run
/// order). Implementing <see cref="IAsyncLifetime"/> directly on the test class
/// gives every test method its own fresh container, since xUnit constructs a new
/// instance of the test class for each test.
/// </summary>
public class PendingMigrationsHealthCheckTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("efcore_healthchecks_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Reports_unhealthy_when_the_InitialCreate_migration_has_not_been_applied()
    {
        await using var db = CreateContext();
        var check = new PendingMigrationsHealthCheck(db);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("InitialCreate");
    }

    [Fact]
    public async Task Reports_healthy_once_the_migration_has_been_applied()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var check = new PendingMigrationsHealthCheck(db);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Reports_unhealthy_rather_than_throwing_when_the_database_is_unreachable()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=nope;Username=postgres;Password=postgres;Timeout=2")
            .Options;

        await using var db = new AppDbContext(options);
        var check = new PendingMigrationsHealthCheck(db);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
