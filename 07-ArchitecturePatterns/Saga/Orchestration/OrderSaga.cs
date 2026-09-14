namespace Saga.Orchestration;

/// <summary>Shared state threaded through every step.</summary>
public class OrderContext
{
    public OrderContext(string orderId, decimal amount, int quantity)
    {
        OrderId = orderId;
        Amount = amount;
        Quantity = quantity;
    }

    public string OrderId { get; }
    public decimal Amount { get; }
    public int Quantity { get; }

    public string? PaymentId { get; set; }
    public bool StockReserved { get; set; }
    public string? ShipmentId { get; set; }
}

/// <summary>Stand-ins for the services a real saga would call over the network.</summary>
public class InventoryService
{
    public int Stock { get; private set; } = 10;
    public List<string> Operations { get; } = new();

    public Task ReserveAsync(int quantity)
    {
        if (quantity > Stock) throw new InvalidOperationException($"only {Stock} in stock");
        Stock -= quantity;
        Operations.Add($"reserve {quantity}");
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(int quantity)
    {
        Stock += quantity;
        Operations.Add($"release {quantity}");
        return Task.CompletedTask;
    }
}

public class PaymentService
{
    private readonly bool _failOnCharge;
    public List<string> Operations { get; } = new();
    public decimal NetCharged { get; private set; }

    public PaymentService(bool failOnCharge = false) => _failOnCharge = failOnCharge;

    public Task<string> ChargeAsync(decimal amount)
    {
        if (_failOnCharge) throw new InvalidOperationException("card declined");

        NetCharged += amount;
        var id = $"pay-{Guid.NewGuid():N}"[..12];
        Operations.Add($"charge {amount:C}");
        return Task.FromResult(id);
    }

    public Task RefundAsync(string paymentId, decimal amount)
    {
        NetCharged -= amount;
        Operations.Add($"refund {amount:C}");
        return Task.CompletedTask;
    }
}

public class ShippingService
{
    private readonly bool _failOnShip;
    public List<string> Operations { get; } = new();

    public ShippingService(bool failOnShip = false) => _failOnShip = failOnShip;

    public Task<string> ShipAsync(string orderId)
    {
        if (_failOnShip) throw new InvalidOperationException("no courier available");

        Operations.Add($"ship {orderId}");
        return Task.FromResult($"trk-{Guid.NewGuid():N}"[..12]);
    }

    public Task CancelAsync(string shipmentId)
    {
        Operations.Add($"cancel {shipmentId}");
        return Task.CompletedTask;
    }
}

public class ReserveStockStep : ISagaStep<OrderContext>
{
    private readonly InventoryService _inventory;
    public ReserveStockStep(InventoryService inventory) => _inventory = inventory;

    public string Name => "ReserveStock";

    public async Task ExecuteAsync(OrderContext context, CancellationToken cancellationToken)
    {
        await _inventory.ReserveAsync(context.Quantity);
        context.StockReserved = true;
    }

    public async Task CompensateAsync(OrderContext context, CancellationToken cancellationToken)
    {
        // Compensations must be safe to run when the step did not complete.
        if (!context.StockReserved) return;

        await _inventory.ReleaseAsync(context.Quantity);
        context.StockReserved = false;
    }
}

public class ChargePaymentStep : ISagaStep<OrderContext>
{
    private readonly PaymentService _payments;
    public ChargePaymentStep(PaymentService payments) => _payments = payments;

    public string Name => "ChargePayment";

    public async Task ExecuteAsync(OrderContext context, CancellationToken cancellationToken)
        => context.PaymentId = await _payments.ChargeAsync(context.Amount);

    public async Task CompensateAsync(OrderContext context, CancellationToken cancellationToken)
    {
        if (context.PaymentId is null) return;

        await _payments.RefundAsync(context.PaymentId, context.Amount);
        context.PaymentId = null;
    }
}

public class ShipOrderStep : ISagaStep<OrderContext>
{
    private readonly ShippingService _shipping;
    public ShipOrderStep(ShippingService shipping) => _shipping = shipping;

    public string Name => "ShipOrder";

    public async Task ExecuteAsync(OrderContext context, CancellationToken cancellationToken)
        => context.ShipmentId = await _shipping.ShipAsync(context.OrderId);

    public async Task CompensateAsync(OrderContext context, CancellationToken cancellationToken)
    {
        if (context.ShipmentId is null) return;

        await _shipping.CancelAsync(context.ShipmentId);
        context.ShipmentId = null;
    }
}
