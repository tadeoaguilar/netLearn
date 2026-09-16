using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.CloudNative.Tests;

public class SmokeTests
{
    [Fact]
    public async Task Can_start_app_host_and_reach_storefront_through_catalogapi()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost_Solution>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync("storefront", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));

        using var client = app.CreateHttpClient("storefront");
        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
    }
}
