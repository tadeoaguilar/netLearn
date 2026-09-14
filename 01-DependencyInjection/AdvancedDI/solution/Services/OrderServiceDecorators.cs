namespace AdvancedDI.Services;

/// <summary>
/// Part 2: each decorator implements the same interface it wraps, so callers
/// cannot tell the difference and the chain can be assembled in any order.
/// </summary>
public class LoggingOrderServiceDecorator : IOrderService
{
    private readonly IOrderService _inner;
    private readonly TimeProvider _timeProvider;

    // TimeProvider is injected rather than calling DateTime.Now directly, which
    // is what makes the timestamp assertable from a test.
    public LoggingOrderServiceDecorator(IOrderService inner, TimeProvider? timeProvider = null)
    {
        _inner = inner;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void PlaceOrder(string productName, int quantity)
    {
        var now = _timeProvider.GetLocalNow();
        Console.WriteLine($"[LOGGING] Order request received at {now:HH:mm:ss}");
        _inner.PlaceOrder(productName, quantity);
        Console.WriteLine($"[LOGGING] Order request completed");
    }
}

/// <summary>
/// Stops invalid work before it reaches the inner service. Because it can
/// short-circuit, ordering matters: put it outside anything expensive.
/// </summary>
public class ValidationOrderServiceDecorator : IOrderService
{
    private readonly IOrderService _inner;

    public ValidationOrderServiceDecorator(IOrderService inner)
    {
        _inner = inner;
    }

    public void PlaceOrder(string productName, int quantity)
    {
        Console.WriteLine($"[VALIDATION] Validating order...");

        if (string.IsNullOrWhiteSpace(productName))
        {
            Console.WriteLine($"[VALIDATION] FAILED - Product name is required");
            return;
        }

        if (quantity <= 0)
        {
            Console.WriteLine($"[VALIDATION] FAILED - Quantity must be positive");
            return;
        }

        Console.WriteLine($"[VALIDATION] PASSED");
        _inner.PlaceOrder(productName, quantity);
    }
}

/// <summary>
/// Detects repeat orders. This decorator holds state, so its registered
/// lifetime decides whether the cache is per-call, per-scope or app-wide --
/// registering it Transient makes the cache useless.
/// </summary>
public class CachingOrderServiceDecorator : IOrderService
{
    private readonly IOrderService _inner;
    private readonly HashSet<string> _recentOrders = new();

    public CachingOrderServiceDecorator(IOrderService inner)
    {
        _inner = inner;
    }

    public void PlaceOrder(string productName, int quantity)
    {
        var key = $"{productName}-{quantity}";

        if (_recentOrders.Contains(key))
        {
            Console.WriteLine($"[CACHING] Duplicate order detected for {productName}");
        }
        else
        {
            _recentOrders.Add(key);
        }

        _inner.PlaceOrder(productName, quantity);
    }
}
