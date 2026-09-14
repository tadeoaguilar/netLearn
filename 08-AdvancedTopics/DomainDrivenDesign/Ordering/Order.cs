using Ddd.Shared;

namespace Ddd.Ordering;

/// <summary>
/// BOUNDED CONTEXT: Ordering.
///
/// "Order" here means a commercial agreement -- items, prices, totals. The
/// Shipping context has its own Order meaning a physical parcel with a weight
/// and an address. Same word, different model, deliberately not shared.
///
/// Forcing one Order class to serve both is how you get a 60-property god
/// object where half the fields are null depending on who is looking.
/// </summary>
public record ProductId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// An ENTITY inside the aggregate. It has identity (two lines for the same
/// product are distinct) but is not addressable from outside -- you reach it
/// through the Order.
/// </summary>
public class OrderLine
{
    internal OrderLine(ProductId productId, string name, Money unitPrice, int quantity)
    {
        ProductId = productId;
        Name = name;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public ProductId ProductId { get; }
    public string Name { get; }
    public Money UnitPrice { get; }
    public int Quantity { get; private set; }

    public Money LineTotal => UnitPrice.Multiply(Quantity);

    internal void IncreaseBy(int quantity) => Quantity += quantity;
}

public enum OrderStatus { Draft, Placed, Cancelled }

public record OrderPlaced(Guid OrderId, Money Total, DateTimeOffset OccurredAt) : IDomainEvent;
public record OrderCancelled(Guid OrderId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;

public class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines = new();

    private Order(string customerId, string currency)
    {
        CustomerId = customerId;
        Currency = currency;
        Status = OrderStatus.Draft;
    }

    public const int MaxLines = 20;

    public string CustomerId { get; }
    public string Currency { get; }
    public OrderStatus Status { get; private set; }

    // Exposed read-only. Handing out the List would let callers add lines
    // behind the aggregate's back and skip every rule below.
    public IReadOnlyList<OrderLine> Lines => _lines;

    public Money Total => _lines.Aggregate(Money.Zero(Currency), (sum, line) => sum.Add(line.LineTotal));

    public static Order StartFor(string customerId, string currency = "USD")
    {
        if (string.IsNullOrWhiteSpace(customerId)) throw new DomainException("Customer is required.");
        return new Order(customerId, currency);
    }

    public void AddLine(ProductId productId, string name, Money unitPrice, int quantity)
    {
        if (Status != OrderStatus.Draft) throw new DomainException($"Cannot change a {Status} order.");
        if (quantity <= 0) throw new DomainException("Quantity must be positive.");
        if (unitPrice.Currency != Currency)
            throw new DomainException($"Order is in {Currency}; line is in {unitPrice.Currency}.");

        var existing = _lines.FirstOrDefault(l => l.ProductId == productId);

        if (existing is not null)
        {
            existing.IncreaseBy(quantity);
            return;
        }

        // An invariant across the whole collection -- only the root can enforce it.
        if (_lines.Count >= MaxLines)
            throw new DomainException($"An order cannot have more than {MaxLines} lines.");

        _lines.Add(new OrderLine(productId, name, unitPrice, quantity));
    }

    public void Place(DateTimeOffset now)
    {
        if (Status != OrderStatus.Draft) throw new DomainException($"Order is already {Status}.");
        if (_lines.Count == 0) throw new DomainException("Cannot place an empty order.");

        Status = OrderStatus.Placed;
        Raise(new OrderPlaced(Id, Total, now));
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        if (Status == OrderStatus.Cancelled) return;
        if (Status == OrderStatus.Placed && Total.IsGreaterThan(Money.Of(1000, Currency)))
            throw new DomainException("Orders over 1000 require manual cancellation.");

        Status = OrderStatus.Cancelled;
        Raise(new OrderCancelled(Id, reason, now));
    }
}

/// <summary>Named business rules, usable in queries and in decisions alike.</summary>
public class QualifiesForFreeShipping : ISpecification<Order>
{
    private readonly Money _threshold;

    public QualifiesForFreeShipping(Money threshold) => _threshold = threshold;

    public bool IsSatisfiedBy(Order order) => order.Total.IsGreaterThan(_threshold);
}

public class HasMultipleItems : ISpecification<Order>
{
    public bool IsSatisfiedBy(Order order) => order.Lines.Sum(l => l.Quantity) > 1;
}

/// <summary>
/// A DOMAIN SERVICE: logic that belongs to the domain but not to any one
/// aggregate. Pricing across an order and a customer's tier is nobody's
/// private business, so it lives here rather than being forced onto Order.
/// </summary>
public class DiscountCalculator
{
    public Money CalculateDiscount(Order order, string customerTier)
    {
        var rate = customerTier switch
        {
            "gold" => 0.15m,
            "silver" => 0.10m,
            _ => 0m
        };

        if (order.Total.IsGreaterThan(Money.Of(500, order.Currency)))
        {
            rate += 0.05m;
        }

        return Money.Of(order.Total.Amount * rate, order.Currency);
    }
}
