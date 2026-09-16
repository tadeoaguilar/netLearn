using Newtonsoft.Json;

namespace CosmosIndexingAndThroughput.Models;

public class OrderLine
{
    [JsonProperty("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonProperty("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("unitPrice")]
    public decimal UnitPrice { get; set; }
}

// The partition key is /customerId: every order belongs to exactly one
// customer, and this project's queries mostly filter by customer, so this
// keeps the common access pattern single-partition.
public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("orderDate")]
    public DateTimeOffset OrderDate { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = "Pending";

    [JsonProperty("orderLines")]
    public List<OrderLine> OrderLines { get; set; } = new();

    [JsonProperty("totalAmount")]
    public decimal TotalAmount { get; set; }
}
