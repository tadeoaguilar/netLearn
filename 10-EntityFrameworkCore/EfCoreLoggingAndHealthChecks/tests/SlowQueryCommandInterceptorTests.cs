using EfCoreLoggingAndHealthChecks.Data;
using EfCoreLoggingAndHealthChecks.Interceptors;
using EfCoreLoggingAndHealthChecks.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EfCoreLoggingAndHealthChecks.Tests;

/// <summary>
/// Proves <see cref="SlowQueryCommandInterceptor"/> actually measures elapsed
/// command time against a real Postgres (using `pg_sleep` to force a slow query
/// deterministically) rather than trusting the implementation on faith.
/// </summary>
public class SlowQueryCommandInterceptorTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public SlowQueryCommandInterceptorTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var (db, _) = CreateContext();
        await using var _1 = db;
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private (AppDbContext Db, ListLoggerProvider LogProvider) CreateContext()
    {
        var logProvider = new ListLoggerProvider();
        // Not disposed: disposing the factory would dispose the provider mid-test,
        // and the ListLogger it hands out must keep working for the DbContext's
        // whole lifetime. It is tiny and test-scoped, so the leak is harmless.
        var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logProvider));
        var logger = loggerFactory.CreateLogger<SlowQueryCommandInterceptor>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(new SlowQueryCommandInterceptor(logger))
            .Options;

        return (new AppDbContext(options), logProvider);
    }

    [Fact]
    public async Task A_query_slower_than_the_threshold_logs_a_warning()
    {
        var (db, logProvider) = CreateContext();
        await using var _1 = db;

        var sleepSeconds = SlowQueryCommandInterceptor.SlowQueryThreshold.TotalSeconds + 0.3;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_sleep({sleepSeconds})");

        logProvider.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Slow query"));
    }

    [Fact]
    public async Task A_fast_query_logs_nothing()
    {
        var (db, logProvider) = CreateContext();
        await using var _1 = db;

        await db.Products.AsNoTracking().ToListAsync();

        logProvider.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
    }
}
