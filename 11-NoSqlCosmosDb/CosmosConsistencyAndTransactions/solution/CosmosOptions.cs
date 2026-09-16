namespace CosmosConsistencyAndTransactions;

/// <summary>
/// Bound from the "Cosmos" configuration section (appsettings.json, or
/// Aspire-injected configuration when run through AppHost).
/// </summary>
public class CosmosOptions
{
    public string Database { get; set; } = "cosmostransactions";
    public string CustomersContainer { get; set; } = "customers";
    public string OrdersContainer { get; set; } = "orders";

    /// <summary>
    /// One of Strong, BoundedStaleness, Session, ConsistentPrefix, Eventual.
    /// Parsed into a real <see cref="Microsoft.Azure.Cosmos.ConsistencyLevel"/>
    /// in Program.cs -- kept as a string here so it round-trips cleanly
    /// through JSON configuration.
    /// </summary>
    public string ConsistencyLevel { get; set; } = "Session";
}
