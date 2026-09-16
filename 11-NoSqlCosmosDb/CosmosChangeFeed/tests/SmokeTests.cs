using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CosmosChangeFeed.Tests;

/// <summary>
/// The one test in this suite that does NOT use the shared
/// <see cref="CosmosEmulatorFixture"/>: it exists purely to check that the
/// AppHost itself is wired correctly (the emulator resource comes up, and a
/// connection string comes out of it), independent of anything about
/// change feed processing.
/// </summary>
public class SmokeTests
{
    [Fact]
    public async Task Can_start_app_host_and_resolve_cosmos_connection_string()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CosmosChangeFeed_AppHost>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications
            .WaitForResourceAsync("cosmos", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));

        var connectionString = await app.GetConnectionStringAsync("cosmos");
        connectionString.Should().NotBeNullOrEmpty();
    }
}
