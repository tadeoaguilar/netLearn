using Microsoft.Azure.Cosmos;

namespace CosmosChangeFeed.Tests;

/// <summary>
/// Exercises <see cref="OrderChangeHandler"/> directly, bypassing the change
/// feed processor entirely. This is what lets the idempotency tests below
/// be fast and deterministic: rather than trying to coax the SDK into
/// genuinely redelivering a change (which depends on timing and internal
/// checkpointing you don't control from a test), we call the handler
/// ourselves with the same input more than once -- which is exactly what a
/// redelivery looks like from the handler's point of view. It has no way to
/// tell "this is the second time I've seen this change" from "a human
/// process called me twice"; that indistinguishability is the whole reason
/// the handler has to be idempotent in the first place.
/// </summary>
[Collection("Cosmos emulator")]
public class OrderChangeHandlerTests
{
    private readonly CosmosEmulatorFixture _fixture;

    public OrderChangeHandlerTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handling_the_same_change_twice_does_not_double_count_totals()
    {
        var customerId = $"customer-{Guid.NewGuid():N}";
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            OrderDate = DateTimeOffset.UtcNow,
            Status = "Placed",
            OrderLines = [new OrderLine { ProductId = "p1", ProductName = "Widget", Quantity = 1, UnitPrice = 15m }],
            TotalAmount = 15m,
        };

        // The order has to actually exist in the orders container: the
        // handler recomputes from source data (see OrderChangeHandler's
        // remarks), it doesn't trust the fields on the changed document it
        // was handed. Writing it once up front mirrors what the processor
        // would have already done before invoking the handler.
        await _fixture.OrdersContainer.CreateItemAsync(order, new PartitionKey(customerId));

        var handler = new OrderChangeHandler(_fixture.OrdersContainer, _fixture.SummaryContainer);

        // Simulate at-least-once redelivery: handle the exact same change
        // twice in a row, as would happen if the processor crashed after
        // the first HandleChangesAsync call returned but before it
        // checkpointed the lease.
        await handler.HandleChangesAsync([order], CancellationToken.None);
        await handler.HandleChangesAsync([order], CancellationToken.None);

        var summary = await _fixture.SummaryContainer.ReadItemAsync<CustomerOrderSummary>(
            customerId, new PartitionKey(customerId));

        summary.Resource.TotalOrders.Should().Be(1);
        summary.Resource.TotalSpent.Should().Be(15m);
    }

    [Fact]
    public async Task Handling_an_empty_batch_is_a_no_op()
    {
        var handler = new OrderChangeHandler(_fixture.OrdersContainer, _fixture.SummaryContainer);

        // Should complete without throwing, and without touching any
        // customer's summary.
        var act = () => handler.HandleChangesAsync([], CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handling_a_batch_with_multiple_customers_updates_each_summary_independently()
    {
        var customerA = $"customer-{Guid.NewGuid():N}";
        var customerB = $"customer-{Guid.NewGuid():N}";

        var orderA = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerA,
            OrderDate = DateTimeOffset.UtcNow,
            TotalAmount = 30m,
        };
        var orderB = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerB,
            OrderDate = DateTimeOffset.UtcNow,
            TotalAmount = 70m,
        };

        await _fixture.OrdersContainer.CreateItemAsync(orderA, new PartitionKey(customerA));
        await _fixture.OrdersContainer.CreateItemAsync(orderB, new PartitionKey(customerB));

        var handler = new OrderChangeHandler(_fixture.OrdersContainer, _fixture.SummaryContainer);

        // One batch, two customers -- as the change feed would deliver if
        // both orders landed within the same poll interval.
        await handler.HandleChangesAsync([orderA, orderB], CancellationToken.None);

        var summaryA = await _fixture.SummaryContainer.ReadItemAsync<CustomerOrderSummary>(customerA, new PartitionKey(customerA));
        var summaryB = await _fixture.SummaryContainer.ReadItemAsync<CustomerOrderSummary>(customerB, new PartitionKey(customerB));

        summaryA.Resource.TotalSpent.Should().Be(30m);
        summaryB.Resource.TotalSpent.Should().Be(70m);
    }

    [Fact]
    public async Task Summary_document_id_matches_the_customer_id()
    {
        var customerId = $"customer-{Guid.NewGuid():N}";
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            OrderDate = DateTimeOffset.UtcNow,
            TotalAmount = 42m,
        };

        await _fixture.OrdersContainer.CreateItemAsync(order, new PartitionKey(customerId));

        var handler = new OrderChangeHandler(_fixture.OrdersContainer, _fixture.SummaryContainer);
        await handler.HandleChangesAsync([order], CancellationToken.None);

        var summary = await _fixture.SummaryContainer.ReadItemAsync<CustomerOrderSummary>(
            customerId, new PartitionKey(customerId));

        // Using the customer id as the summary document's own id is what
        // makes the upsert a point write instead of a query-then-write --
        // see the remarks on CustomerOrderSummary.
        summary.Resource.Id.Should().Be(customerId);
        summary.Resource.CustomerId.Should().Be(customerId);
    }

    [Fact]
    public async Task Recomputing_after_a_second_order_reflects_both_orders_not_just_the_latest_change()
    {
        var customerId = $"customer-{Guid.NewGuid():N}";
        var handler = new OrderChangeHandler(_fixture.OrdersContainer, _fixture.SummaryContainer);

        var firstOrder = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            OrderDate = DateTimeOffset.UtcNow.AddMinutes(-5),
            TotalAmount = 11m,
        };
        await _fixture.OrdersContainer.CreateItemAsync(firstOrder, new PartitionKey(customerId));
        await handler.HandleChangesAsync([firstOrder], CancellationToken.None);

        var secondOrder = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            OrderDate = DateTimeOffset.UtcNow,
            TotalAmount = 22m,
        };
        await _fixture.OrdersContainer.CreateItemAsync(secondOrder, new PartitionKey(customerId));

        // The change feed only tells the handler about the SECOND order
        // here, yet because the handler recomputes from every order in the
        // customer's partition -- not from the changed document alone --
        // the summary reflects both.
        await handler.HandleChangesAsync([secondOrder], CancellationToken.None);

        var summary = await _fixture.SummaryContainer.ReadItemAsync<CustomerOrderSummary>(
            customerId, new PartitionKey(customerId));

        summary.Resource.TotalOrders.Should().Be(2);
        summary.Resource.TotalSpent.Should().Be(33m);
    }
}
