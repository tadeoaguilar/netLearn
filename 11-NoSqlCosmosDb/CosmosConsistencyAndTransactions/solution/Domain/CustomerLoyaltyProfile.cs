using Newtonsoft.Json;

namespace CosmosConsistencyAndTransactions.Domain;

/// <summary>
/// A denormalized, per-customer summary that lives in the SAME container
/// AND partition key as that customer's <see cref="Order"/> documents --
/// the "orders" container, partitioned by /customerId -- rather than in the
/// "customers" container, which is partitioned by /id.
///
/// This split is the whole point of Part 4. Cosmos DB's <c>TransactionalBatch</c>
/// only guarantees atomicity for operations against documents that share
/// BOTH a container and a partition key value. The master <see cref="Customer"/>
/// record (Parts 1-3) lives in a different container under a different
/// partition key (/id, not /customerId) and so can never take part in a
/// batch with an Order -- there is no way to make "insert this order AND
/// update the master Customer row" atomic without changing the data model.
///
/// Redesigning the partition key -- keeping a second, purpose-built copy of
/// just the fields a same-partition transaction needs -- is the standard
/// real-world answer to that constraint, and is exactly what this type is.
/// See Part 5 for what to do when even that isn't an option (e.g. the two
/// documents that must move together belong to two different customers).
/// </summary>
public class CustomerLoyaltyProfile
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; } = "CustomerLoyaltyProfile";

    [JsonProperty("loyaltyPoints")]
    public int LoyaltyPoints { get; set; }

    [JsonProperty("lifetimeOrderCount")]
    public int LifetimeOrderCount { get; set; }

    [JsonProperty("lifetimeSpend")]
    public decimal LifetimeSpend { get; set; }

    [JsonProperty("_etag")]
    public string? ETag { get; set; }

    public static string BuildId(string customerId) => $"profile-{customerId}";
}
