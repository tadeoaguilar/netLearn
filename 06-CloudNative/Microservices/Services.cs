using Polly;
using Polly.CircuitBreaker;

namespace CloudNative.Microservices;

public record Product(string Sku, string Name, decimal Price);
public record InventoryLevel(string Sku, int Available);
public record ProductDetail(string Sku, string Name, decimal Price, int? Available, string InventoryStatus);

/// <summary>
/// Stands in for a separate deployable service reached over HTTP. Its failure
/// modes are configurable so the gateway's resilience can be demonstrated.
/// </summary>
public class InventoryClient
{
    private readonly Dictionary<string, int> _levels = new()
    {
        ["SKU-1"] = 42,
        ["SKU-2"] = 0,
        ["SKU-3"] = 7
    };

    public bool IsDown { get; set; }
    public TimeSpan Latency { get; set; } = TimeSpan.FromMilliseconds(20);
    public int Calls { get; private set; }

    public async Task<InventoryLevel> GetAsync(string sku, CancellationToken cancellationToken)
    {
        Calls++;
        await Task.Delay(Latency, cancellationToken);

        if (IsDown) throw new HttpRequestException("inventory service unreachable");

        return new InventoryLevel(sku, _levels.GetValueOrDefault(sku));
    }
}

public class CatalogueClient
{
    private readonly Dictionary<string, Product> _products = new()
    {
        ["SKU-1"] = new Product("SKU-1", "Mechanical keyboard", 149.99m),
        ["SKU-2"] = new Product("SKU-2", "Ultrawide monitor", 699m),
        ["SKU-3"] = new Product("SKU-3", "Desk mat", 29m)
    };

    public Task<Product?> GetAsync(string sku, CancellationToken cancellationToken)
        => Task.FromResult(_products.GetValueOrDefault(sku));

    public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Product>>(_products.Values.OrderBy(p => p.Sku).ToList());
}

/// <summary>
/// The composing service. It calls two others and merges the result -- the
/// classic gateway/BFF shape.
///
/// The important design decision: inventory is a SOFT dependency. If it is
/// down, the product page still renders with the price and a note that stock
/// is unknown, rather than returning 500. A microservice architecture where
/// every dependency is hard is just a distributed monolith with worse latency.
/// </summary>
public class ProductComposer
{
    private readonly CatalogueClient _catalogue;
    private readonly InventoryClient _inventory;
    private readonly ResiliencePipeline<InventoryLevel?> _inventoryPipeline;

    public ProductComposer(CatalogueClient catalogue, InventoryClient inventory)
    {
        _catalogue = catalogue;
        _inventory = inventory;

        _inventoryPipeline = new ResiliencePipelineBuilder<InventoryLevel?>()
            // Fallback outermost: whatever fails below, the caller gets null
            // rather than an exception.
            .AddFallback(new Polly.Fallback.FallbackStrategyOptions<InventoryLevel?>
            {
                FallbackAction = _ => Outcome.FromResultAsValueTask<InventoryLevel?>(null)
            })
            // One circuit per DEPENDENCY, not per request. All three products
            // in a /products call share it, which is what you want: the
            // inventory service is either healthy or it is not.
            //
            // Expect a half-open step on recovery. When BreakDuration expires
            // the breaker lets ONE trial call through; if it succeeds the
            // circuit closes and the rest follow. So the first /products after
            // an outage may show one product with stock and the others still
            // "unavailable". That is the breaker working, not a bug.
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<InventoryLevel?>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromSeconds(5)
            })
            .AddRetry(new Polly.Retry.RetryStrategyOptions<InventoryLevel?>
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(20),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddTimeout(TimeSpan.FromMilliseconds(500))
            .Build();
    }

    public async Task<ProductDetail?> GetAsync(string sku, CancellationToken cancellationToken)
    {
        // Hard dependency: no catalogue, no product. This one is allowed to 404.
        var product = await _catalogue.GetAsync(sku, cancellationToken);
        if (product is null) return null;

        var level = await _inventoryPipeline.ExecuteAsync(
            async token => (InventoryLevel?)await _inventory.GetAsync(sku, token),
            cancellationToken);

        return new ProductDetail(
            product.Sku,
            product.Name,
            product.Price,
            level?.Available,
            level is null ? "unavailable" : level.Available > 0 ? "in stock" : "out of stock");
    }

    public async Task<IReadOnlyList<ProductDetail>> ListAsync(CancellationToken cancellationToken)
    {
        var products = await _catalogue.ListAsync(cancellationToken);

        // Fan out concurrently rather than calling inventory in a loop. Serial
        // calls here are the single most common cause of a slow gateway.
        var details = await Task.WhenAll(products.Select(p => GetAsync(p.Sku, cancellationToken)));

        return details.Where(d => d is not null).Select(d => d!).ToList();
    }
}
