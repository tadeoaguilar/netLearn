using AdvancedDI.Services;

namespace AdvancedDI.Tests;

/// <summary>Part 2: decorators add behaviour and can short-circuit the chain.</summary>
public class DecoratorTests
{
    /// <summary>Test double that records what reached the innermost service.</summary>
    private sealed class RecordingOrderService : IOrderService
    {
        public List<(string Product, int Quantity)> Received { get; } = new();

        public void PlaceOrder(string productName, int quantity)
            => Received.Add((productName, quantity));
    }

    [Fact]
    public void Validation_passes_a_valid_order_through_to_the_inner_service()
    {
        var inner = new RecordingOrderService();
        var sut = new ValidationOrderServiceDecorator(inner);

        sut.PlaceOrder("Laptop", 2);

        inner.Received.Should().ContainSingle()
            .Which.Should().Be(("Laptop", 2));
    }

    [Theory]
    [InlineData("", 5)]
    [InlineData("   ", 5)]
    [InlineData("Laptop", 0)]
    [InlineData("Laptop", -1)]
    public void Validation_blocks_an_invalid_order_from_reaching_the_inner_service(
        string product, int quantity)
    {
        var inner = new RecordingOrderService();
        var sut = new ValidationOrderServiceDecorator(inner);

        sut.PlaceOrder(product, quantity);

        inner.Received.Should().BeEmpty("an invalid order must not reach the service");
    }

    [Fact]
    public void Caching_still_forwards_a_duplicate_but_notices_it()
    {
        // The caching decorator reports duplicates rather than suppressing them.
        // Asserting the actual behaviour beats asserting what the name implies.
        var inner = new RecordingOrderService();
        var sut = new CachingOrderServiceDecorator(inner);

        sut.PlaceOrder("Laptop", 2);
        sut.PlaceOrder("Laptop", 2);

        inner.Received.Should().HaveCount(2);
    }

    [Fact]
    public void The_chain_applies_decorators_from_the_outside_in()
    {
        var inner = new RecordingOrderService();
        IOrderService chain = new LoggingOrderServiceDecorator(
            new ValidationOrderServiceDecorator(
                new CachingOrderServiceDecorator(inner)));

        chain.PlaceOrder("Laptop", 2);
        chain.PlaceOrder("", 1);          // stopped by validation
        chain.PlaceOrder("Monitor", 3);

        inner.Received.Should().Equal(("Laptop", 2), ("Monitor", 3));
    }

    [Fact]
    public void Ordering_matters_validation_outside_caching_never_caches_junk()
    {
        var inner = new RecordingOrderService();

        // Validation outermost: the invalid order is rejected before caching.
        var cache = new CachingOrderServiceDecorator(inner);
        IOrderService validationOutside = new ValidationOrderServiceDecorator(cache);

        validationOutside.PlaceOrder("", 1);
        validationOutside.PlaceOrder("Laptop", 2);

        inner.Received.Should().ContainSingle().Which.Should().Be(("Laptop", 2));
    }
}
