using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.CloudNative.Tests;

// The three extra tests EXERCISE.md Part A.7 asks the learner to write,
// implemented for real against the reference solution (solution/AppHost).
// Each one checks a different piece of what Aspire actually wired up for
// CatalogApi rather than just "the app starts" (that's SmokeTests.cs).
//
// Like SmokeTests.cs, these start the real DistributedApplication -- real
// Postgres and Redis containers, real HTTP calls -- so they require Docker
// to run. They are not runnable in an environment with no container
// runtime; they only need to build there.
public class CatalogApiTests
{
    private static async Task<DistributedApplication> StartAppAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost_Solution>();
        var app = await appHost.BuildAsync();
        await app.StartAsync();

        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync("catalogapi", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));

        return app;
    }

    [Fact]
    public async Task Products_endpoint_returns_the_seeded_product_count()
    {
        await using var app = await StartAppAsync();
        using var client = app.CreateHttpClient("catalogapi");

        // AppHost's seed-product-count parameter defaults to "10"
        // (AppHost/Program.cs), and CatalogApi only seeds when the
        // Products table is empty, so a fresh container should come up
        // with exactly that many rows.
        var products = await client.GetFromJsonAsync<List<ProductDto>>("/products");

        products.Should().NotBeNull();
        products!.Should().HaveCount(10);
    }

    [Fact]
    public async Task Products_endpoint_serves_the_second_request_from_the_output_cache()
    {
        await using var app = await StartAppAsync();
        using var client = app.CreateHttpClient("catalogapi");

        // First call is a cache miss: CatalogApi queries Postgres and
        // CacheOutput() stores the response in Redis for 30 seconds
        // (see the .CacheOutput(...) policy on GET /products).
        var first = await client.GetAsync("/products");
        first.EnsureSuccessStatusCode();

        // A second call immediately after should be served from Redis
        // instead of hitting Postgres again. ASP.NET Core's output caching
        // middleware marks a cache hit by adding an "Age" response header
        // (RFC 7234 semantics) -- present when replaying a cached
        // response, absent on the miss that created it.
        var second = await client.GetAsync("/products");
        second.EnsureSuccessStatusCode();

        second.Headers.TryGetValues("Age", out _).Should().BeTrue(
            "the output cache should have served this response from Redis instead of Postgres");
    }

    [Fact]
    public async Task Health_and_alive_endpoints_both_report_healthy()
    {
        await using var app = await StartAppAsync();
        using var client = app.CreateHttpClient("catalogapi");

        // /health runs every registered check, including the Postgres and
        // Redis checks Aspire's client integrations add automatically.
        // /alive only runs the dependency-free "self" check (see
        // ServiceDefaults/Extensions.cs). Both should report healthy once
        // the resource itself has reported Running.
        var health = await client.GetAsync("/health");
        var alive = await client.GetAsync("/alive");

        health.EnsureSuccessStatusCode();
        alive.EnsureSuccessStatusCode();

        (await health.Content.ReadAsStringAsync()).Should().Be("Healthy");
        (await alive.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    private record ProductDto(int Id, string Name, decimal Price);
}
