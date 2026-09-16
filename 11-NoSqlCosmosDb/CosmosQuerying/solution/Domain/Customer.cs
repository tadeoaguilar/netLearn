namespace CosmosQuerying.Domain;

/// <summary>
/// A customer document, stored in the "customers" container with partition
/// key path "/id" -- each customer is its own single-item partition. That's
/// a deliberate, common Cosmos modeling choice for small reference entities
/// that are almost always looked up by id and never queried "give me every
/// customer in partition X": there is no better partition key than the id
/// itself when there's no other natural grouping key.
/// </summary>
public class Customer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>"Standard", "Silver", or "Gold".</summary>
    public string Tier { get; set; } = "Standard";
}
