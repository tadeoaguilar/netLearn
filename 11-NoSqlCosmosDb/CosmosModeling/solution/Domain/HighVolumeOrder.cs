using Newtonsoft.Json;

namespace CosmosModeling.Domain;

/// <summary>
/// A hypothetical variant of <see cref="Order"/> for a very high-volume customer (Part 5 in
/// EXERCISE.md). Container: <c>orders-highvolume</c>, partition key: <c>/partitionKey</c> --
/// a <b>synthetic, composite</b> key built from <see cref="CustomerId"/> and the order's
/// year-month, e.g. <c>"cust-123:2026-09"</c>, instead of <see cref="CustomerId"/> alone.
///
/// This bounds any one partition's growth to a customer's orders within a single month --
/// a customer who places 50,000 orders a year is spread across 12 partitions instead of one
/// ever-growing partition. The cost: "give me ALL of this customer's orders, all time" is no
/// longer a single-partition query -- it now has to either fan out across every month
/// partition that customer has ever used, or the application maintains a separate index of
/// which months to look in. That's the trade made deliberately here, and only for customers
/// active enough to need it -- an ordinary customer (Part 2's <see cref="Order"/>) has no
/// reason to pay this cost.
/// </summary>
public class HighVolumeOrder
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string CustomerId { get; set; } = string.Empty;

    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;

    public List<OrderLine> OrderLines { get; set; } = [];

    /// <summary>
    /// The synthetic partition key itself, stored as a plain property so it's queryable and
    /// visible on the document -- Cosmos partition keys are always drawn from a real JSON
    /// property, never computed implicitly. Build it with <see cref="BuildPartitionKey"/> so
    /// every writer forms it identically.
    /// </summary>
    [JsonProperty("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>Combines customer and year-month into one synthetic partition key string.</summary>
    public static string BuildPartitionKey(string customerId, DateTimeOffset orderDate) =>
        $"{customerId}:{orderDate:yyyy-MM}";
}
