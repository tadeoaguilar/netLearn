namespace CosmosModeling.Domain;

/// <summary>
/// Added to <see cref="Order"/> after the domain shipped its first documents -- see Part 4
/// (schema evolution) in EXERCISE.md. Orders written before this type existed simply don't
/// have a <c>ShippingAddress</c> field in their stored JSON at all.
/// </summary>
public class ShippingAddress
{
    public string Line1 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;
}
