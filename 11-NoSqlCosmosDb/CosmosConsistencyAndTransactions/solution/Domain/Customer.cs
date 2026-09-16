using Newtonsoft.Json;

namespace CosmosConsistencyAndTransactions.Domain;

/// <summary>
/// The master customer record. Lives in the "customers" container,
/// partitioned by /id -- one document per partition. Used in Parts 1-3
/// (consistency levels, session tokens, ETag optimistic concurrency).
/// </summary>
public class Customer
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("loyaltyPoints")]
    public int LoyaltyPoints { get; set; }

    [JsonProperty("tier")]
    public string Tier { get; set; } = "Standard";

    /// <summary>
    /// Cosmos DB assigns and rewrites this on every write. Never set it
    /// yourself -- it's the concurrency token used in Part 3.
    /// </summary>
    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}
