namespace CosmosQuerying.Domain;

/// <summary>
/// A single line item, embedded directly inside an <see cref="Order"/>
/// document -- there is no separate "orderLines" container. Cosmos favors
/// embedding data that is always read and written together with its parent,
/// which line items are: nobody ever asks for "all order lines across every
/// order" the way a relational report might.
/// </summary>
public class OrderLine
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Stored explicitly (computed once at write time) rather than as a
    /// get-only computed property, so it round-trips through JSON exactly
    /// like every other field instead of silently disappearing on
    /// deserialization.
    /// </summary>
    public decimal LineTotal { get; set; }
}
