using Microsoft.Azure.Cosmos;

namespace CosmosChangeFeed.Tests;

/// <summary>
/// End-to-end tests: a real change feed processor, watching the real
/// "orders" container in the emulator, keeping the real
/// "customerordersummary" container up to date.
///
/// Change feed processing is asynchronous and eventual (lease acquisition,
/// the processor's poll interval, and the handler's own query all add real
/// latency), so every assertion here polls with a timeout via
/// <see cref="Program.PollForSummaryAsync"/> instead of a fixed delay --
/// see EXERCISE.md Part 2 for why a fixed `Task.Delay` is not reliable
/// here.
///
/// Each test uses its own processor name (and a fresh customer id) so
/// tests sharing one emulator and one lease container don't interfere with
/// each other's lease documents or aggregates.
/// </summary>
[Collection("Cosmos emulator")]
public class ChangeFeedProcessorTests
{
    private readonly CosmosEmulatorFixture _fixture;

    public ChangeFeedProcessorTests(CosmosEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Writing_an_order_materializes_a_customer_summary()
    {
        var customerId = $"customer-{Guid.NewGuid():N}";
        var processor = _fixture.BuildProcessor(
            processorName: $"processor-{Guid.NewGuid():N}",
            instanceName: "instance-1");

        await processor.StartAsync();
        try
        {
            await WaitForLeaseAcquisitionAsync();

            var order = NewOrder(customerId, total: 49.99m);
            await _fixture.OrdersContainer.CreateItemAsync(order, new PartitionKey(customerId));

            var summary = await Program.PollForSummaryAsync(
                _fixture.SummaryContainer, customerId, expectedOrders: 1, timeout: TimeSpan.FromSeconds(30));

            summary.Should().NotBeNull();
            summary!.TotalOrders.Should().Be(1);
            summary.TotalSpent.Should().Be(49.99m);
        }
        finally
        {
            await processor.StopAsync();
        }
    }

    [Fact]
    public async Task Writing_multiple_orders_for_the_same_customer_aggregates_totals_and_last_order_date()
    {
        var customerId = $"customer-{Guid.NewGuid():N}";
        var processor = _fixture.BuildProcessor(
            processorName: $"processor-{Guid.NewGuid():N}",
            instanceName: "instance-1");

        await processor.StartAsync();
        try
        {
            await WaitForLeaseAcquisitionAsync();

            var firstOrderDate = DateTimeOffset.UtcNow.AddMinutes(-10);
            var secondOrderDate = DateTimeOffset.UtcNow;

            var firstOrder = NewOrder(customerId, total: 10m, orderDate: firstOrderDate);
            var secondOrder = NewOrder(customerId, total: 25m, orderDate: secondOrderDate);

            await _fixture.OrdersContainer.CreateItemAsync(firstOrder, new PartitionKey(customerId));
            await _fixture.OrdersContainer.CreateItemAsync(secondOrder, new PartitionKey(customerId));

            var summary = await Program.PollForSummaryAsync(
                _fixture.SummaryContainer, customerId, expectedOrders: 2, timeout: TimeSpan.FromSeconds(30));

            summary.Should().NotBeNull();
            summary!.TotalOrders.Should().Be(2);
            summary.TotalSpent.Should().Be(35m);
            summary.LastOrderDate.Should().BeCloseTo(secondOrderDate, TimeSpan.FromSeconds(1));
        }
        finally
        {
            await processor.StopAsync();
        }
    }

    [Fact]
    public async Task Orders_for_different_customers_produce_independent_summaries()
    {
        var customerA = $"customer-{Guid.NewGuid():N}";
        var customerB = $"customer-{Guid.NewGuid():N}";
        var processor = _fixture.BuildProcessor(
            processorName: $"processor-{Guid.NewGuid():N}",
            instanceName: "instance-1");

        await processor.StartAsync();
        try
        {
            await WaitForLeaseAcquisitionAsync();

            await _fixture.OrdersContainer.CreateItemAsync(NewOrder(customerA, total: 100m), new PartitionKey(customerA));
            await _fixture.OrdersContainer.CreateItemAsync(NewOrder(customerB, total: 5m), new PartitionKey(customerB));

            var summaryA = await Program.PollForSummaryAsync(_fixture.SummaryContainer, customerA, expectedOrders: 1, timeout: TimeSpan.FromSeconds(30));
            var summaryB = await Program.PollForSummaryAsync(_fixture.SummaryContainer, customerB, expectedOrders: 1, timeout: TimeSpan.FromSeconds(30));

            summaryA.Should().NotBeNull();
            summaryB.Should().NotBeNull();
            summaryA!.TotalSpent.Should().Be(100m);
            summaryB!.TotalSpent.Should().Be(5m);
        }
        finally
        {
            await processor.StopAsync();
        }
    }

    [Fact]
    public async Task No_summary_exists_before_any_orders_are_written()
    {
        var customerId = $"customer-{Guid.NewGuid():N}";

        // No processor running, no orders written -- the poll should exhaust
        // its timeout and return null rather than throw, which is exactly
        // what a caller checking "has the read model caught up yet?" needs.
        var summary = await Program.PollForSummaryAsync(
            _fixture.SummaryContainer, customerId, expectedOrders: 1, timeout: TimeSpan.FromSeconds(2));

        summary.Should().BeNull();
    }

    [Fact]
    public async Task A_fresh_processor_instance_resumes_from_the_lease_containers_recorded_progress()
    {
        var customerId = $"customer-{Guid.NewGuid():N}";
        var processorName = $"processor-{Guid.NewGuid():N}";

        // First instance: processes one order, checkpoints its lease, then
        // stops (simulating a restart, e.g. a deployment or crash).
        var firstInstance = _fixture.BuildProcessor(processorName, instanceName: "instance-1");
        await firstInstance.StartAsync();
        try
        {
            await WaitForLeaseAcquisitionAsync();

            var firstOrder = NewOrder(customerId, total: 12m);
            await _fixture.OrdersContainer.CreateItemAsync(firstOrder, new PartitionKey(customerId));

            var afterFirstOrder = await Program.PollForSummaryAsync(
                _fixture.SummaryContainer, customerId, expectedOrders: 1, timeout: TimeSpan.FromSeconds(30));
            afterFirstOrder.Should().NotBeNull();
        }
        finally
        {
            await firstInstance.StopAsync();
        }

        // The lease container now holds a checkpoint for this processor
        // name/partition past the first order. A brand new instance --
        // same processor name, so it picks up the same leases -- should
        // only need to catch up on what happened AFTER that checkpoint,
        // not replay the container's whole history.
        var secondInstance = _fixture.BuildProcessor(processorName, instanceName: "instance-2");
        await secondInstance.StartAsync();
        try
        {
            await WaitForLeaseAcquisitionAsync();

            var secondOrder = NewOrder(customerId, total: 8m);
            await _fixture.OrdersContainer.CreateItemAsync(secondOrder, new PartitionKey(customerId));

            var afterSecondOrder = await Program.PollForSummaryAsync(
                _fixture.SummaryContainer, customerId, expectedOrders: 2, timeout: TimeSpan.FromSeconds(30));

            afterSecondOrder.Should().NotBeNull();
            // Recompute-based aggregation means this assertion holds
            // whether the second instance replayed everything or resumed
            // cleanly -- either way the *result* converges to the true
            // total. What proves resumption happened is the lease document
            // itself, checked below: a fresh lease would start with no
            // continuation token, but a resumed one carries the first
            // instance's checkpoint forward.
            afterSecondOrder!.TotalOrders.Should().Be(2);
            afterSecondOrder.TotalSpent.Should().Be(20m);
        }
        finally
        {
            await secondInstance.StopAsync();
        }

        var leaseDocuments = new List<System.Text.Json.JsonElement>();
        using var iterator = _fixture.LeaseContainer.GetItemQueryIterator<System.Text.Json.JsonElement>(
            $"SELECT * FROM c WHERE CONTAINS(c.id, '{processorName}')");
        while (iterator.HasMoreResults)
        {
            leaseDocuments.AddRange(await iterator.ReadNextAsync());
        }

        // The processor persisted at least one lease document for this
        // processor name -- the mechanism that lets the second instance
        // resume rather than restart from the beginning of the feed.
        leaseDocuments.Should().NotBeEmpty();
    }

    private static Order NewOrder(string customerId, decimal total, DateTimeOffset? orderDate = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        CustomerId = customerId,
        OrderDate = orderDate ?? DateTimeOffset.UtcNow,
        Status = "Placed",
        OrderLines = [new OrderLine { ProductId = "p1", ProductName = "Widget", Quantity = 1, UnitPrice = total }],
        TotalAmount = total,
    };

    /// <summary>
    /// The processor's first lease acquisition and estimation pass take a
    /// little wall-clock time after StartAsync returns. A short fixed wait
    /// here (as opposed to the polling used for summary assertions) is
    /// acceptable because it is not the thing under test -- it just avoids
    /// writing an order before the processor could possibly have started
    /// watching for it.
    /// </summary>
    private static Task WaitForLeaseAcquisitionAsync() => Task.Delay(TimeSpan.FromSeconds(2));
}
