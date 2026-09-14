using Saga.Orchestration;

namespace Saga.Demos;

public static class Demos
{
    private static SagaOrchestrator<OrderContext> Build(
        InventoryService inventory, PaymentService payments, ShippingService shipping) =>
        new([
            new ReserveStockStep(inventory),
            new ChargePaymentStep(payments),
            new ShipOrderStep(shipping)
        ], Console.WriteLine);

    public static async Task Part1HappyPath()
    {
        Console.WriteLine("=== PART 1: ALL STEPS SUCCEED ===\n");

        var inventory = new InventoryService();
        var payments = new PaymentService();
        var shipping = new ShippingService();
        var saga = Build(inventory, payments, shipping);

        var result = await saga.ExecuteAsync(new OrderContext("ORD-1", 99.99m, 2));

        Console.WriteLine($"\nSucceeded: {result.Succeeded}");
        Console.WriteLine($"Completed: {string.Join(", ", result.Completed)}");
        Console.WriteLine($"Stock left: {inventory.Stock}, net charged: {payments.NetCharged:C}");
    }

    public static async Task Part2CompensationOnFailure()
    {
        Console.WriteLine("=== PART 2: THE LAST STEP FAILS ===\n");

        var inventory = new InventoryService();
        var payments = new PaymentService();
        var shipping = new ShippingService(failOnShip: true);
        var saga = Build(inventory, payments, shipping);

        var result = await saga.ExecuteAsync(new OrderContext("ORD-2", 99.99m, 2));

        Console.WriteLine($"\nSucceeded: {result.Succeeded}  (failed at {result.FailedStep}: {result.Error})");
        Console.WriteLine($"Compensated, in reverse: {string.Join(", ", result.Compensated)}");
        Console.WriteLine($"\nStock back to {inventory.Stock}, net charged {payments.NetCharged:C}");
        Console.WriteLine($"Payment operations: {string.Join(" then ", payments.Operations)}");
        Console.WriteLine("\nThe customer WAS charged and then refunded. That is not the same");
        Console.WriteLine("as never being charged -- they saw both on their statement. A saga");
        Console.WriteLine("buys consistency, not invisibility.");
    }

    public static async Task Part3FailureInTheMiddle()
    {
        Console.WriteLine("=== PART 3: A MIDDLE STEP FAILS ===\n");

        var inventory = new InventoryService();
        var payments = new PaymentService(failOnCharge: true);
        var shipping = new ShippingService();
        var saga = Build(inventory, payments, shipping);

        var result = await saga.ExecuteAsync(new OrderContext("ORD-3", 99.99m, 2));

        Console.WriteLine($"\nFailed at: {result.FailedStep}");
        Console.WriteLine($"Completed before failure: {string.Join(", ", result.Completed)}");
        Console.WriteLine($"Compensated: {string.Join(", ", result.Compensated)}");
        Console.WriteLine($"\nStock restored to {inventory.Stock}; nothing shipped: {shipping.Operations.Count == 0}");
        Console.WriteLine("Only steps that actually ran were compensated. Shipping never");
        Console.WriteLine("started, so there was nothing to cancel.");
    }

    public static async Task Part4NothingToCompensate()
    {
        Console.WriteLine("=== PART 4: THE FIRST STEP FAILS ===\n");

        var inventory = new InventoryService();
        var payments = new PaymentService();
        var shipping = new ShippingService();
        var saga = Build(inventory, payments, shipping);

        // 99 units requested, 10 in stock.
        var result = await saga.ExecuteAsync(new OrderContext("ORD-4", 99.99m, 99));

        Console.WriteLine($"\nFailed at: {result.FailedStep} ({result.Error})");
        Console.WriteLine($"Compensations run: {result.Compensated.Count}");
        Console.WriteLine($"Stock untouched: {inventory.Stock}, nothing charged: {payments.NetCharged:C}");
        Console.WriteLine("\nFailing early is the cheapest outcome. Order your steps so the");
        Console.WriteLine("most likely failure comes first and there is least to undo.");
    }
}
