using Newtonsoft.Json;

namespace CosmosModeling.Domain;

/// <summary>
/// A customer document. Container: <c>customers</c>, partition key: <c>/id</c>.
///
/// Customers are almost always looked up one at a time, by their own id -- there's no
/// "give me all customers" query that matters in this domain the way "give me this
/// customer's orders" does for <see cref="Order"/>. Partitioning by the item's own id
/// gives Cosmos the best possible distribution (every customer is, by definition, a
/// different partition key value) and turns every lookup into a cheap single-partition
/// point read.
/// </summary>
public class Customer
{
    /// <summary>
    /// Cosmos requires every item to have an "id" property (lowercase, exactly that name)
    /// that is unique within its partition. <see cref="JsonPropertyAttribute"/> maps this
    /// PascalCase C# property onto that required lowercase JSON field.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
