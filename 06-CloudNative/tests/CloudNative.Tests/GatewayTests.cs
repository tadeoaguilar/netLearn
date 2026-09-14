using CloudNative.Microservices;

namespace CloudNative.Tests;

/// <summary>
/// A gateway's most important property: a soft dependency failing must degrade
/// the response, not fail it.
/// </summary>
public class GatewayTests
{
    private static ProductComposer Build(InventoryClient inventory)
        => new(new CatalogueClient(), inventory);

    [Fact]
    public async Task A_healthy_gateway_composes_both_services()
    {
        var composer = Build(new InventoryClient());

        var product = await composer.GetAsync("SKU-1", default);

        product!.Name.Should().Be("Mechanical keyboard");
        product.Available.Should().Be(42);
        product.InventoryStatus.Should().Be("in stock");
    }

    [Fact]
    public async Task Zero_stock_is_out_of_stock_not_unavailable()
    {
        // A real zero must not be confused with "we could not find out".
        var composer = Build(new InventoryClient());

        var product = await composer.GetAsync("SKU-2", default);

        product!.Available.Should().Be(0);
        product.InventoryStatus.Should().Be("out of stock");
    }

    [Fact]
    public async Task A_soft_dependency_failing_degrades_rather_than_throws()
    {
        var inventory = new InventoryClient { IsDown = true };
        var composer = Build(inventory);

        var product = await composer.GetAsync("SKU-1", default);

        product.Should().NotBeNull("the catalogue still answered");
        product!.Price.Should().Be(149.99m);
        product.Available.Should().BeNull();
        product.InventoryStatus.Should().Be("unavailable");
    }

    [Fact]
    public async Task A_slow_dependency_is_timed_out_rather_than_waited_on()
    {
        var inventory = new InventoryClient { Latency = TimeSpan.FromSeconds(30) };
        var composer = Build(inventory);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var product = await composer.GetAsync("SKU-1", default);

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        product!.InventoryStatus.Should().Be("unavailable");
    }

    [Fact]
    public async Task A_hard_dependency_missing_the_item_still_reports_not_found()
    {
        var composer = Build(new InventoryClient());

        var product = await composer.GetAsync("SKU-DOES-NOT-EXIST", default);

        product.Should().BeNull("the catalogue is a hard dependency; no product means 404");
    }

    [Fact]
    public async Task Listing_fans_out_concurrently_rather_than_in_a_loop()
    {
        // Three products at 200ms each: serial would be ~600ms.
        var inventory = new InventoryClient { Latency = TimeSpan.FromMilliseconds(200) };
        var composer = Build(inventory);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var products = await composer.ListAsync(default);

        products.Should().HaveCount(3);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task The_circuit_breaker_stops_calling_a_failing_dependency()
    {
        var inventory = new InventoryClient { IsDown = true };
        var composer = Build(inventory);

        for (var i = 0; i < 5; i++) await composer.ListAsync(default);

        var callsAfterBreak = inventory.Calls;
        await composer.ListAsync(default);

        inventory.Calls.Should().Be(callsAfterBreak,
            "an open circuit rejects without contacting the service at all");
    }
}
