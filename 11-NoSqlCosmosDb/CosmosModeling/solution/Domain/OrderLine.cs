namespace CosmosModeling.Domain;

/// <summary>
/// One line item on an <see cref="Order"/>. This type is never stored in its own
/// container or read independently -- it only ever exists embedded inside an
/// <see cref="Order"/> document. See Part 3 in EXERCISE.md for why that's a deliberate
/// choice, not an oversight.
/// </summary>
public class OrderLine
{
    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
