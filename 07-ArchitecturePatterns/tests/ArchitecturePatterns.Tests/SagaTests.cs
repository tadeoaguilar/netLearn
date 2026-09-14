using Saga.Orchestration;

namespace ArchitecturePatterns.Tests;

public class SagaTests
{
    private static SagaOrchestrator<OrderContext> Build(
        InventoryService inventory, PaymentService payments, ShippingService shipping) =>
        new([new ReserveStockStep(inventory), new ChargePaymentStep(payments), new ShipOrderStep(shipping)]);

    [Fact]
    public async Task All_steps_succeeding_leaves_no_compensation()
    {
        var inventory = new InventoryService();
        var payments = new PaymentService();
        var saga = Build(inventory, payments, new ShippingService());

        var result = await saga.ExecuteAsync(new OrderContext("O-1", 100m, 2));

        result.Succeeded.Should().BeTrue();
        result.Compensated.Should().BeEmpty();
        inventory.Stock.Should().Be(8);
        payments.NetCharged.Should().Be(100m);
    }

    [Fact]
    public async Task A_failure_compensates_completed_steps_in_reverse_order()
    {
        // Reverse order matters: a later step may depend on an earlier one.
        var inventory = new InventoryService();
        var payments = new PaymentService();
        var saga = Build(inventory, payments, new ShippingService(failOnShip: true));

        var result = await saga.ExecuteAsync(new OrderContext("O-1", 100m, 2));

        result.Succeeded.Should().BeFalse();
        result.FailedStep.Should().Be("ShipOrder");
        result.Compensated.Should().Equal("ChargePayment", "ReserveStock");
    }

    [Fact]
    public async Task Compensation_restores_the_observable_state()
    {
        var inventory = new InventoryService();
        var payments = new PaymentService();
        var saga = Build(inventory, payments, new ShippingService(failOnShip: true));

        await saga.ExecuteAsync(new OrderContext("O-1", 100m, 3));

        inventory.Stock.Should().Be(10, "the reservation was released");
        payments.NetCharged.Should().Be(0m, "the charge was refunded");
    }

    [Fact]
    public async Task Compensation_is_not_the_same_as_never_happening()
    {
        // The customer saw a charge AND a refund. A saga buys consistency,
        // not invisibility -- this test exists to make that concrete.
        var payments = new PaymentService();
        var saga = Build(new InventoryService(), payments, new ShippingService(failOnShip: true));

        await saga.ExecuteAsync(new OrderContext("O-1", 100m, 1));

        payments.Operations.Should().Equal("charge $100.00", "refund $100.00");
        payments.NetCharged.Should().Be(0m);
    }

    [Fact]
    public async Task Only_steps_that_actually_ran_are_compensated()
    {
        var inventory = new InventoryService();
        var shipping = new ShippingService();
        var saga = Build(inventory, new PaymentService(failOnCharge: true), shipping);

        var result = await saga.ExecuteAsync(new OrderContext("O-1", 100m, 2));

        result.FailedStep.Should().Be("ChargePayment");
        result.Compensated.Should().Equal("ReserveStock");
        shipping.Operations.Should().BeEmpty("shipping never started, so there is nothing to cancel");
    }

    [Fact]
    public async Task A_first_step_failure_compensates_nothing()
    {
        var inventory = new InventoryService();
        var payments = new PaymentService();
        var saga = Build(inventory, payments, new ShippingService());

        var result = await saga.ExecuteAsync(new OrderContext("O-1", 100m, 99));

        result.Completed.Should().BeEmpty();
        result.Compensated.Should().BeEmpty();
        inventory.Stock.Should().Be(10);
        payments.NetCharged.Should().Be(0m);
    }
}
