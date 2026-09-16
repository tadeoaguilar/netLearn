using Newtonsoft.Json;

namespace CosmosConsistencyAndTransactions.Domain;

/// <summary>
/// Lives in the "orders" container, partitioned by /customerId, alongside
/// that same customer's <see cref="CustomerLoyaltyProfile"/> document --
/// see CustomerLoyaltyProfile's remarks for why.
/// </summary>
public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; } = "Order";

    [JsonProperty("lines")]
    public List<OrderLine> Lines { get; set; } = [];

    [JsonProperty("status")]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [JsonProperty("totalAmount")]
    public decimal TotalAmount => Lines.Sum(l => l.Quantity * l.UnitPrice);

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}
