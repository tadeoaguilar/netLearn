using Newtonsoft.Json;

namespace CosmosChangeFeed;

/// <summary>
/// Container: "customers", partition key "/id". Kept for the same
/// retail-orders shape used by the other 11-NoSqlCosmosDb projects, even
/// though this project's change feed processor only watches "orders".
/// </summary>
public class Customer
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Container: "orders", partition key "/customerId". This is the container
/// the change feed processor watches -- every insert or update to an Order
/// document is what drives the CustomerOrderSummary read model.
/// </summary>
public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public DateTimeOffset OrderDate { get; set; }

    public string Status { get; set; } = "Placed";

    public List<OrderLine> OrderLines { get; set; } = new();

    public decimal TotalAmount { get; set; }
}

public class OrderLine
{
    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

/// <summary>
/// Container: "customerordersummary", partition key "/customerId". A
/// materialized read model kept up to date by the change feed processor,
/// one document per customer, so reading a customer's order totals never
/// requires a live cross-partition aggregation query.
///
/// Its own "id" is the customer id -- there is exactly one summary document
/// per customer, and using the customer id as the document id makes the
/// upsert in the change handler a plain point write (read + replace by id
/// within the partition), not a query-then-write.
/// </summary>
public class CustomerOrderSummary
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public int TotalOrders { get; set; }

    public decimal TotalSpent { get; set; }

    public DateTimeOffset? LastOrderDate { get; set; }
}
