using Ddd.Shared;

namespace Ddd.Shipping;

/// <summary>
/// BOUNDED CONTEXT: Shipping.
///
/// This context also has an "Order", and it means something different: a parcel
/// with a weight and an address. It does not know about prices, currencies or
/// discounts, because nothing here needs them.
///
/// The two contexts meet through an ANTI-CORRUPTION LAYER (see
/// OrderingToShippingTranslator), never by sharing a class.
/// </summary>
public record Address(string Line1, string City, string PostCode, string Country)
{
    public static Address Create(string line1, string city, string postCode, string country)
    {
        if (string.IsNullOrWhiteSpace(line1)) throw new DomainException("Address line 1 is required.");
        if (string.IsNullOrWhiteSpace(postCode)) throw new DomainException("Post code is required.");
        return new Address(line1, city, postCode, country);
    }
}

public readonly record struct Weight
{
    public decimal Kilograms { get; }

    private Weight(decimal kilograms) => Kilograms = kilograms;

    public static Weight OfKilograms(decimal kilograms)
    {
        if (kilograms <= 0) throw new DomainException("Weight must be positive.");
        return new Weight(kilograms);
    }

    public Weight Add(Weight other) => new(Kilograms + other.Kilograms);
}

public enum ShipmentStatus { Pending, Dispatched, Delivered }

public class Shipment : AggregateRoot
{
    private Shipment(string externalOrderReference, Address destination, Weight weight)
    {
        ExternalOrderReference = externalOrderReference;
        Destination = destination;
        Weight = weight;
        Status = ShipmentStatus.Pending;
    }

    /// <summary>
    /// A reference, not a foreign key to another aggregate's object. Aggregates
    /// reference each other BY IDENTITY -- holding the other object would blur
    /// the transactional boundary between them.
    /// </summary>
    public string ExternalOrderReference { get; }

    public Address Destination { get; }
    public Weight Weight { get; }
    public ShipmentStatus Status { get; private set; }
    public string? TrackingNumber { get; private set; }

    public static Shipment For(string orderReference, Address destination, Weight weight)
        => new(orderReference, destination, weight);

    public void Dispatch(string trackingNumber)
    {
        if (Status != ShipmentStatus.Pending) throw new DomainException($"Shipment is already {Status}.");
        if (string.IsNullOrWhiteSpace(trackingNumber)) throw new DomainException("Tracking number is required.");

        TrackingNumber = trackingNumber;
        Status = ShipmentStatus.Dispatched;
    }

    public void MarkDelivered()
    {
        if (Status != ShipmentStatus.Dispatched) throw new DomainException("Only a dispatched shipment can be delivered.");
        Status = ShipmentStatus.Delivered;
    }
}

/// <summary>
/// The ANTI-CORRUPTION LAYER between Ordering and Shipping.
///
/// It takes the Ordering context's language and translates it into Shipping's,
/// so neither model has to bend to accommodate the other. Without it, Shipping
/// would slowly acquire prices and currencies it has no use for.
/// </summary>
public static class OrderingToShippingTranslator
{
    public static Shipment ToShipment(
        Ordering.Order order,
        Address destination,
        Func<Ordering.ProductId, decimal> weightLookup)
    {
        if (order.Status != Ordering.OrderStatus.Placed)
            throw new DomainException("Only a placed order can be shipped.");

        // Money, currency and discounts do not cross this boundary. Only the
        // facts Shipping actually needs: a reference, a destination, a weight.
        var totalKilograms = order.Lines.Sum(line => weightLookup(line.ProductId) * line.Quantity);

        return Shipment.For(
            order.Id.ToString(),
            destination,
            Weight.OfKilograms(totalKilograms));
    }
}
