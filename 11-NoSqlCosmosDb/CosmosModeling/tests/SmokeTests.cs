using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CosmosModeling.Tests;

public class SmokeTests
{
    [Fact]
    public async Task Can_start_app_host_and_resolve_cosmos_connection_string()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CosmosModeling_AppHost>();
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
