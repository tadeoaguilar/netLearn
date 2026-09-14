namespace AdvancedDI.Services;

/// <summary>
/// Part 2: the service that decorators will wrap. Note that it knows nothing
/// about logging, validation or caching -- that is the point of the pattern.
/// </summary>
public interface IOrderService
{
    void PlaceOrder(string productName, int quantity);
}

public class OrderService : IOrderService
{
    public void PlaceOrder(string productName, int quantity)
    {
        Console.WriteLine($"[OrderService] Placing order: {quantity}x {productName}");
    }
}
