using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Persistence;

/// <summary>
/// Ensures the database and both containers exist. When this app runs under
/// the AppHost, Aspire's Cosmos DB resource already provisions the database
/// and containers declared in <c>AppHost/Program.cs</c> before this project
/// even starts -- but exactly like EfCoreQuerying's <c>EnsureCreatedAsync</c>,
/// this project should also work unmodified against a manually-started
/// emulator with nothing pre-provisioned. <c>CreateIfNotExistsAsync</c> makes
/// both paths safe: a no-op when Aspire already created things, and the
/// actual provisioning step when it didn't.
/// </summary>
public static class CosmosInitializer
{
    public const string DatabaseName = "cosmosquerying";
    public const string CustomersContainer = "customers";
    public const string OrdersContainer = "orders";

    public static async Task<Database> EnsureDatabaseAsync(CosmosClient client, CancellationToken cancellationToken = default)
    {
        var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(DatabaseName, cancellationToken: cancellationToken);
        var database = databaseResponse.Database;

        // Partition key paths must match the camelCase JSON field names the
        // CosmosSerialization policy produces from Customer.Id and
        // Order.CustomerId -- "/id" and "/customerId".
        await database.CreateContainerIfNotExistsAsync(CustomersContainer, "/id", cancellationToken: cancellationToken);
        await database.CreateContainerIfNotExistsAsync(OrdersContainer, "/customerId", cancellationToken: cancellationToken);

        return database;
    }
}
