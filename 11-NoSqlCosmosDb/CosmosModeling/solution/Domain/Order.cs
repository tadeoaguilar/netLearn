using Newtonsoft.Json;

namespace CosmosModeling.Domain;

/// <summary>
/// An order document, with its line items <b>embedded</b> rather than stored separately.
/// Container: <c>orders</c>, partition key: <c>/customerId</c>.
///
/// <b>Why /customerId:</b> the dominant query in this domain is "give me this customer's
/// orders" (an order history page, a support lookup, a re-order flow) -- partitioning by
/// <see cref="CustomerId"/> keeps every order a customer has ever placed co-located in one
/// logical partition, so that query never has to fan out across the container. The
/// trade-off: a single, extremely high-volume customer keeps writing into that same
/// logical partition forever, and Cosmos caps a logical partition at 20GB of storage and a
/// share of the container's overall throughput. Part 5 in EXERCISE.md shows a synthetic key
/// that bounds this for that scenario.
///
/// <b>Why OrderLine[] is embedded:</b> see Part 3 in EXERCISE.md.
/// </summary>
public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The partition key property. Must serialize to exactly "customerId" (lowercase c) to
    /// match the container's partition key path (<c>/customerId</c>) declared in
    /// AppHost/Program.cs -- partition key paths are matched against the JSON on the wire,
    /// not the CLR property name.
    /// </summary>
    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;

    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Embedded, not referenced -- order lines are always read and written together with
    /// their order and are never queried on their own. See Part 3 in EXERCISE.md.
    /// </summary>
    public List<OrderLine> OrderLines { get; set; } = [];

    /// <summary>
    /// A document-version convention (Part 4 in EXERCISE.md): bump this whenever a change to
    /// the shape of <see cref="Order"/> is significant enough that reader code needs to
    /// branch on it deliberately, rather than just tolerating an optional field being absent.
    /// Version 1 was the original shape (no <see cref="ShippingAddress"/>); version 2 added
    /// it. A document with no SchemaVersion field at all (an even older shape) deserializes
    /// this to its C# default, 0, which reader code can treat as "version 1 or earlier".
    /// </summary>
    public int SchemaVersion { get; set; } = 2;

    /// <summary>
    /// Added in SchemaVersion 2. Orders written by older code simply don't have this property
    /// in their stored JSON -- there is no migration step in Cosmos that goes back and adds
    /// it. Nullable here specifically so that reader code is forced to consider the "missing"
    /// case instead of assuming every document has it.
    /// </summary>
    public ShippingAddress? ShippingAddress { get; set; }
}
