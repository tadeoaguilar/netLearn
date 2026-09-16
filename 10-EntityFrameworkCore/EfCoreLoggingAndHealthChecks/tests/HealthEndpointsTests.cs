using System.Net;
using System.Text.Json;
using EfCoreLoggingAndHealthChecks.Tests.Infrastructure;

namespace EfCoreLoggingAndHealthChecks.Tests;

/// <summary>
/// The happy path: a real Postgres, migrated, reachable. Both probes should
/// report Healthy.
/// </summary>
public class HealthEndpointsTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres = new();
    private TestApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private static async Task<(HttpStatusCode Status, string HealthStatus)> GetHealthAsync(
        HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        var status = JsonDocument.Parse(body).RootElement.GetProperty("status").GetString()!;
        return (response.StatusCode, status);
    }

    [Fact]
    public async Task Health_ready_is_healthy_when_the_database_is_reachable_and_migrated()
    {
        var client = _factory.CreateClient();

        var (statusCode, healthStatus) = await GetHealthAsync(client, "/health/ready");

        statusCode.Should().Be(HttpStatusCode.OK);
        healthStatus.Should().Be("Healthy");
    }

    [Fact]
    public async Task Health_live_is_healthy_and_does_not_depend_on_the_database()
    {
        var client = _factory.CreateClient();

        var (statusCode, healthStatus) = await GetHealthAsync(client, "/health/live");

        statusCode.Should().Be(HttpStatusCode.OK);
        healthStatus.Should().Be("Healthy");
    }
}

/// <summary>
/// The red path: the app is pointed at an unreachable database. This is the
/// scenario the whole liveness/readiness split exists for -- the process itself
/// is fine and must keep saying so, while the readiness probe (correctly) fails
/// so an orchestrator stops routing traffic here.
/// </summary>
public class HealthEndpointsRedPathTests : IClassFixture<HealthEndpointsRedPathTests.Fixture>
{
    private readonly TestApiFactory _factory;

    public HealthEndpointsRedPathTests(Fixture fixture) => _factory = fixture.Factory;

    private static async Task<(HttpStatusCode Status, string HealthStatus)> GetHealthAsync(
        HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        var status = JsonDocument.Parse(body).RootElement.GetProperty("status").GetString()!;
        return (response.StatusCode, status);
    }

    [Fact]
    public async Task Health_ready_is_unhealthy_when_the_database_is_unreachable()
    {
        var client = _factory.CreateClient();

        var (statusCode, healthStatus) = await GetHealthAsync(client, "/health/ready");

        statusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        healthStatus.Should().Be("Unhealthy");
    }

    [Fact]
    public async Task Health_live_stays_healthy_even_though_the_database_is_unreachable()
    {
        var client = _factory.CreateClient();

        var (statusCode, healthStatus) = await GetHealthAsync(client, "/health/live");

        statusCode.Should().Be(HttpStatusCode.OK);
        healthStatus.Should().Be("Healthy");
    }

    /// <summary>No container needed: the whole point is that the database is
    /// never reachable, so a fixed bogus address is enough. A short timeout
    /// keeps the failing checks fast instead of hanging on TCP retries.</summary>
    public class Fixture : IDisposable
    {
        public TestApiFactory Factory { get; } = new(
            "Host=127.0.0.1;Port=1;Database=nope;Username=postgres;Password=postgres;Timeout=2");

        public void Dispose() => Factory.Dispose();
    }
}
