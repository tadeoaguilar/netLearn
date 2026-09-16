namespace CosmosQuerying.Domain;

/// <summary>
/// An order document, stored in the "orders" container with partition key
/// path "/customerId" -- every query in this exercise that filters by
/// customer can be scoped to exactly one physical partition (Part 3), and
/// "this customer's order history" is by far the most common read pattern
/// for an orders container, so it's the right partition key even though it
/// means a cross-customer report (Part 3's cross-partition example) has to
/// fan out across partitions.
/// </summary>
public class Order
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }

    /// <summary>"Pending", "Shipped", "Delivered", or "Cancelled".</summary>
    public string Status { get; set; } = "Pending";

    public List<OrderLine> Lines { get; set; } = [];
    public decimal TotalAmount { get; set; }
}
