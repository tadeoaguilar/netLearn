using Ddd.Ordering;
using Ddd.Shared;
using Ddd.Shipping;

namespace AdvancedTopics.Tests;

public class DddTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_value_object_is_equal_by_value()
    {
        Money.Of(5m).Should().Be(Money.Of(5m));
        Money.Of(5m, "USD").Should().NotBe(Money.Of(5m, "EUR"));
    }

    [Fact]
    public void A_value_object_cannot_be_constructed_invalid()
    {
        FluentActions.Invoking(() => Money.Of(-1m)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => Money.Of(1m, "DOLLARS")).Should().Throw<DomainException>();
    }

    [Fact]
    public void Adding_different_currencies_is_refused()
    {
        // The reason Money exists rather than decimal.
        var act = () => Money.Of(10m, "USD").Add(Money.Of(10m, "EUR"));

        act.Should().Throw<DomainException>().WithMessage("*Cannot add EUR to USD*");
    }

    [Fact]
    public void Money_rounds_half_away_from_zero_not_to_even()
    {
        // .NET's default is banker's rounding (ToEven), which would give 10.00
        // here. Money.Of states MidpointRounding explicitly for this reason.
        Money.Of(10.005m).Amount.Should().Be(10.01m);
        Money.Of(10.015m).Amount.Should().Be(10.02m);
        Money.Of(10.004m).Amount.Should().Be(10.00m);
    }

    [Fact]
    public void An_aggregate_merges_a_repeated_product_instead_of_duplicating_it()
    {
        var order = Order.StartFor("cust-1");

        order.AddLine(new ProductId("KB-1"), "Keyboard", Money.Of(100m), 1);
        order.AddLine(new ProductId("KB-1"), "Keyboard", Money.Of(100m), 2);

        order.Lines.Should().ContainSingle();
        order.Lines[0].Quantity.Should().Be(3);
        order.Total.Should().Be(Money.Of(300m));
    }

    [Fact]
    public void An_aggregate_enforces_an_invariant_across_its_whole_collection()
    {
        var order = Order.StartFor("cust-1");

        for (var i = 0; i < Order.MaxLines; i++)
        {
            order.AddLine(new ProductId($"P-{i}"), $"Product {i}", Money.Of(10m), 1);
        }

        var act = () => order.AddLine(new ProductId("ONE-MORE"), "Too many", Money.Of(10m), 1);

        act.Should().Throw<DomainException>().WithMessage($"*{Order.MaxLines} lines*");
    }

    [Fact]
    public void An_empty_order_cannot_be_placed()
    {
        var act = () => Order.StartFor("cust-1").Place(Now);

        act.Should().Throw<DomainException>().WithMessage("*empty order*");
    }

    [Fact]
    public void Placing_an_order_records_a_domain_event()
    {
        var order = Order.StartFor("cust-1");
        order.AddLine(new ProductId("KB-1"), "Keyboard", Money.Of(100m), 1);

        order.Place(Now);

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderPlaced>()
            .Which.Total.Should().Be(Money.Of(100m));
    }

    [Fact]
    public void A_placed_order_cannot_be_modified()
    {
        var order = Order.StartFor("cust-1");
        order.AddLine(new ProductId("KB-1"), "Keyboard", Money.Of(100m), 1);
        order.Place(Now);

        var act = () => order.AddLine(new ProductId("X"), "Late", Money.Of(5m), 1);

        act.Should().Throw<DomainException>().WithMessage("*Placed order*");
    }

    [Fact]
    public void Specifications_compose()
    {
        var order = Order.StartFor("cust-1");
        order.AddLine(new ProductId("KB-1"), "Keyboard", Money.Of(600m), 1);

        var freeShipping = new QualifiesForFreeShipping(Money.Of(500m));
        var multipleItems = new HasMultipleItems();

        freeShipping.IsSatisfiedBy(order).Should().BeTrue();
        multipleItems.IsSatisfiedBy(order).Should().BeFalse();
        new AndSpecification<Order>(freeShipping, multipleItems).IsSatisfiedBy(order).Should().BeFalse();
    }

    [Fact]
    public void A_domain_service_holds_logic_that_belongs_to_no_single_aggregate()
    {
        var order = Order.StartFor("cust-1");
        order.AddLine(new ProductId("KB-1"), "Keyboard", Money.Of(1000m), 1);

        var calculator = new DiscountCalculator();

        // gold 15% + 5% for being over 500
        calculator.CalculateDiscount(order, "gold").Should().Be(Money.Of(200m));
        calculator.CalculateDiscount(order, "bronze").Should().Be(Money.Of(50m));
    }

    [Fact]
    public void The_anti_corruption_layer_translates_between_contexts()
    {
        var order = Order.StartFor("cust-1");
        order.AddLine(new ProductId("KB-1"), "Keyboard", Money.Of(100m), 2);
        order.Place(Now);

        var shipment = OrderingToShippingTranslator.ToShipment(
            order,
            Address.Create("1 Main St", "Springfield", "12345", "US"),
            _ => 1.5m);

        shipment.Weight.Kilograms.Should().Be(3.0m);
        shipment.ExternalOrderReference.Should().Be(order.Id.ToString(),
            "contexts reference each other by identity, not by object");
    }

    [Fact]
    public void An_unplaced_order_cannot_be_shipped()
    {
        var order = Order.StartFor("cust-1");
        order.AddLine(new ProductId("KB-1"), "Keyboard", Money.Of(100m), 1);

        var act = () => OrderingToShippingTranslator.ToShipment(
            order, Address.Create("1 Main St", "Springfield", "12345", "US"), _ => 1m);

        act.Should().Throw<DomainException>();
    }
}
