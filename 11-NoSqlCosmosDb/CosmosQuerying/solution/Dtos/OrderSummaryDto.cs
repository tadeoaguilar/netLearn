namespace CosmosQuerying.Dtos;

/// <summary>
/// A projection shape carrying only the three fields Part 5's "narrow
/// SELECT list" demos and tests need. Deliberately has no
/// <c>Lines</c>/<c>Status</c>/<c>OrderDate</c> properties at all -- the point
/// of a projection is that the fields you didn't ask for never cross the
/// network in the first place, so there's nothing here to hold them even if
/// you wanted to.
/// </summary>
public class OrderSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}
