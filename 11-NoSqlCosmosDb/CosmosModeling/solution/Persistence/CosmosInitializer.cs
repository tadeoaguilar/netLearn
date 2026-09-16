using Microsoft.Azure.Cosmos;

namespace CosmosModeling.Persistence;

/// <summary>
/// Idempotently ensures the database and containers this project needs exist.
///
/// When run via <c>AppHost</c>, Aspire already provisions these against the emulator from
/// the <c>AddCosmosDatabase</c>/<c>AddContainer</c> calls in AppHost/Program.cs, so these
/// calls are no-ops. When run standalone against a manually-started emulator (see the
/// module README), nothing has created them yet -- this is what makes that path work
/// unmodified.
/// </summary>
public static class CosmosInitializer
{
    public const string DatabaseId = "cosmosmodeling";
    public const string CustomersContainerId = "customers";
    public const string OrdersContainerId = "orders";
    public const string HighVolumeOrdersContainerId = "orders-highvolume";

    public static async Task<Database> EnsureDatabaseAsync(CosmosClient client)
    {
        var response = await client.CreateDatabaseIfNotExistsAsync(DatabaseId);
        return response.Database;
    }

    /// <summary>Partition key: /id -- see Customer.cs.</summary>
    public static async Task<Container> EnsureCustomersContainerAsync(Database database)
    {
        var response = await database.CreateContainerIfNotExistsAsync(CustomersContainerId, "/id");
        return response.Container;
    }

    /// <summary>Partition key: /customerId -- see Order.cs.</summary>
    public static async Task<Container> EnsureOrdersContainerAsync(Database database)
    {
        var response = await database.CreateContainerIfNotExistsAsync(OrdersContainerId, "/customerId");
        return response.Container;
    }

    /// <summary>Partition key: /partitionKey (synthetic) -- see HighVolumeOrder.cs.</summary>
    public static async Task<Container> EnsureHighVolumeOrdersContainerAsync(Database database)
    {
        var response = await database.CreateContainerIfNotExistsAsync(HighVolumeOrdersContainerId, "/partitionKey");
        return response.Container;
    }
}
