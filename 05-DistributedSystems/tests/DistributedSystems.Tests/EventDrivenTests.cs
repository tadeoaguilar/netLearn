using EventDriven.Bus;
using EventDriven.Services;

namespace DistributedSystems.Tests;

public class EventDrivenTests
{
    [Fact]
    public async Task Publishing_with_no_subscribers_is_not_an_error()
    {
        var bus = new EventBus();

        var act = async () => await bus.PublishAsync(new OrderPlaced("O-1", "c-1", 10m));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Every_subscriber_receives_the_event()
    {
        // The key difference from a queue: an event bus broadcasts.
        var bus = new EventBus();
        var received = 0;

        for (var i = 0; i < 3; i++)
        {
            bus.Subscribe<OrderPlaced>($"sub-{i}", (_, _) => { Interlocked.Increment(ref received); return Task.CompletedTask; });
        }

        await bus.PublishAsync(new OrderPlaced("O-1", "c-1", 10m));

        received.Should().Be(3);
    }

    [Fact]
    public async Task A_chain_of_events_runs_end_to_end()
    {
        var bus = new EventBus();
        var orders = new OrderService(bus);
        var payments = new PaymentService(bus);
        var shipping = new ShippingService(bus);

        await orders.PlaceOrderAsync("O-1", "c-1", 99m);

        payments.Charged.Should().Contain("O-1");
        shipping.Shipped.Should().Contain("O-1", "shipping reacted to an event payments emitted");
    }

    [Fact]
    public async Task A_failing_subscriber_does_not_stop_the_others()
    {
        var bus = new EventBus();
        var orders = new OrderService(bus);
        _ = new FlakyAuditService(bus);
        var payments = new PaymentService(bus);
        var analytics = new AnalyticsService(bus);

        await orders.PlaceOrderAsync("O-1", "c-1", 25m);

        payments.Charged.Should().ContainSingle();
        analytics.OrderCount.Should().Be(1);
        bus.Log.Should().Contain(l => l.Contains("handler failed"));
    }

    [Fact]
    public async Task A_new_subscriber_requires_no_change_to_the_publisher()
    {
        var bus = new EventBus();
        var orders = new OrderService(bus);
        var analytics = new AnalyticsService(bus);

        await orders.PlaceOrderAsync("O-1", "c-1", 30m);
        await orders.PlaceOrderAsync("O-2", "c-2", 70m);

        analytics.OrderCount.Should().Be(2);
        analytics.Revenue.Should().Be(100m);
    }
}
