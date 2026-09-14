namespace Ddd.Shared;

/// <summary>
/// A VALUE OBJECT: defined by its values, not an identity. Two Money instances
/// of $5 USD are interchangeable in a way that two Customers named "Ada" are not.
///
/// Being immutable and self-validating means an invalid Money cannot exist, so
/// nothing downstream re-checks it.
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Of(decimal amount, string currency = "USD")
    {
        if (amount < 0) throw new DomainException("Money cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("Currency must be a 3-letter code.");

        // MidpointRounding is stated explicitly. The default is ToEven
        // (banker's rounding), so decimal.Round(10.005m, 2) gives 10.00, not
        // 10.01 -- a difference that becomes real money at invoice volume.
        // Whichever rule your domain uses, never leave it to the default.
        return new Money(
            decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            currency.ToUpperInvariant());
    }

    public static Money Zero(string currency = "USD") => Of(0, currency);

    public Money Add(Money other)
    {
        // The type system cannot stop you adding USD to EUR, so the value
        // object does. A plain `decimal` would have silently allowed it.
        if (Currency != other.Currency)
            throw new DomainException($"Cannot add {other.Currency} to {Currency}.");

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Multiply(int quantity) => new(Amount * quantity, Currency);

    public bool IsGreaterThan(Money other) => Currency == other.Currency && Amount > other.Amount;

    public override string ToString() => $"{Amount:0.00} {Currency}";
}

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// An AGGREGATE ROOT: the only object outside code is allowed to hold a
/// reference to. Everything inside the boundary is reached through it, which is
/// what lets it guarantee invariants spanning several objects.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _events = new();

    public Guid Id { get; protected init; } = Guid.NewGuid();
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    protected void Raise(IDomainEvent @event) => _events.Add(@event);
    public void ClearEvents() => _events.Clear();
}

/// <summary>
/// A SPECIFICATION: a named business rule, testable on its own and combinable.
/// Better than a bare predicate because "OrderQualifiesForFreeShipping" means
/// something to the business, and `o => o.Total.Amount > 50` does not.
/// </summary>
public interface ISpecification<in T>
{
    bool IsSatisfiedBy(T candidate);
}

public class AndSpecification<T> : ISpecification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public AndSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public bool IsSatisfiedBy(T candidate) => _left.IsSatisfiedBy(candidate) && _right.IsSatisfiedBy(candidate);
}
