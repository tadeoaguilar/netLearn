using EventDriven.Bus;

namespace EventDriven.Services;

public record OrderPlaced(string OrderId, string CustomerId, decimal Amount) : EventBase;
public record PaymentTaken(string OrderId, decimal Amount) : EventBase;
public record OrderShipped(string OrderId, string TrackingNumber) : EventBase;

/// <summary>
/// CHOREOGRAPHY: each service reacts to events and emits its own. No central
/// coordinator knows the whole flow.
///
/// Adding a new step means adding a subscriber -- nothing existing changes.
/// The cost is that no single place describes the process, so answering "what
/// happens when an order is placed?" means reading every service.
/// </summary>
public class OrderService
{
    private readonly EventBus _bus;
    public List<string> Placed { get; } = new();

    public OrderService(EventBus bus) => _bus = bus;

    public async Task PlaceOrderAsync(string orderId, string customerId, decimal amount)
    {
        Placed.Add(orderId);
        Console.WriteLine($"[orders]    placed {orderId} for {amount:C}");

        // The order service's job ends here. It does not call payments, does
        // not know payments exists, and does not wait for it.
        await _bus.PublishAsync(new OrderPlaced(orderId, customerId, amount));
    }
}

public class PaymentService
{
    private readonly EventBus _bus;
    public List<string> Charged { get; } = new();

    public PaymentService(EventBus bus)
    {
        _bus = bus;
        bus.Subscribe<OrderPlaced>(nameof(PaymentService), HandleAsync);
    }

    private async Task HandleAsync(OrderPlaced @event, CancellationToken cancellationToken)
    {
        await Task.Delay(20, cancellationToken);
        Charged.Add(@event.OrderId);
        Console.WriteLine($"[payments]  charged {@event.Amount:C} for {@event.OrderId}");

        await _bus.PublishAsync(new PaymentTaken(@event.OrderId, @event.Amount), cancellationToken);
    }
}

public class ShippingService
{
    public List<string> Shipped { get; } = new();

    public ShippingService(EventBus bus)
        => bus.Subscribe<PaymentTaken>(nameof(ShippingService), HandleAsync);

    private async Task HandleAsync(PaymentTaken @event, CancellationToken cancellationToken)
    {
        await Task.Delay(20, cancellationToken);
        Shipped.Add(@event.OrderId);
        Console.WriteLine($"[shipping]  shipped {@event.OrderId}");
    }
}

/// <summary>
/// Added later, subscribing to an event that already existed. Note that no
/// other service changed to make this work -- that is the payoff.
/// </summary>
public class AnalyticsService
{
    public decimal Revenue { get; private set; }
    public int OrderCount { get; private set; }

    public AnalyticsService(EventBus bus)
    {
        bus.Subscribe<OrderPlaced>(nameof(AnalyticsService), (@event, _) =>
        {
            Revenue += @event.Amount;
            OrderCount++;
            return Task.CompletedTask;
        });
    }
}

/// <summary>A subscriber that fails, to show it does not take the others down.</summary>
public class FlakyAuditService
{
    public FlakyAuditService(EventBus bus)
        => bus.Subscribe<OrderPlaced>(nameof(FlakyAuditService), (_, _)
            => throw new InvalidOperationException("audit log unavailable"));
}
