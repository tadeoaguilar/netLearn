using System.Net;
using System.Net.Http.Json;
using EfCoreLoggingAndHealthChecks.Models;
using EfCoreLoggingAndHealthChecks.Tests.Infrastructure;

namespace EfCoreLoggingAndHealthChecks.Tests;

/// <summary>
/// End-to-end through the real minimal API and a real, migrated Postgres. These
/// endpoints exist only to generate genuine EF Core activity for the logging and
/// interceptor parts of the exercise to observe -- so the assertions here are
/// deliberately about "the thing I just created is there", never about the
/// table being empty, since the container (and its data) is shared across every
/// test in this class.
/// </summary>
public class ProductsAndOrdersEndpointTests : IAsyncLifetime
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

    [Fact]
    public async Task Posting_a_product_makes_it_show_up_in_the_product_list()
    {
        var client = _factory.CreateClient();
        var name = $"Widget-{Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("/products", new { name, price = 12.50m });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var products = await client.GetFromJsonAsync<List<Product>>("/products");
        products.Should().ContainSingle(p => p.Name == name && p.Price == 12.50m);
    }

    [Fact]
    public async Task The_audit_interceptor_stamps_CreatedAt_on_a_product_created_through_the_API()
    {
        var client = _factory.CreateClient();
        var name = $"Stamped-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/products", new { name, price = 1m });
        var product = await response.Content.ReadFromJsonAsync<Product>();

        product!.CreatedAt.Should().NotBe(default);
        product.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Posting_an_order_for_an_existing_product_succeeds()
    {
        var client = _factory.CreateClient();
        var productResponse = await client.PostAsJsonAsync(
            "/products", new { name = $"Orderable-{Guid.NewGuid():N}", price = 3m });
        var product = await productResponse.Content.ReadFromJsonAsync<Product>();

        var orderResponse = await client.PostAsJsonAsync(
            "/orders", new { productId = product!.Id, quantity = 4 });

        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await orderResponse.Content.ReadFromJsonAsync<Order>();
        order!.ProductId.Should().Be(product.Id);
        order.Quantity.Should().Be(4);
    }

    [Fact]
    public async Task Posting_an_order_for_a_nonexistent_product_returns_404()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/orders", new { productId = -1, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Posting_an_order_with_a_non_positive_quantity_returns_400()
    {
        var client = _factory.CreateClient();
        var productResponse = await client.PostAsJsonAsync(
            "/products", new { name = $"BadQty-{Guid.NewGuid():N}", price = 3m });
        var product = await productResponse.Content.ReadFromJsonAsync<Product>();

        var response = await client.PostAsJsonAsync(
            "/orders", new { productId = product!.Id, quantity = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
