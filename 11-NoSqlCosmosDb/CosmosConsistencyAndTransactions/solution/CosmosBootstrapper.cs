using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions;

/// <summary>
/// Creates the database and containers this project needs if they don't
/// already exist. When run through AppHost, Aspire already provisioned them
/// (see AppHost/Program.cs) and these calls are no-ops; when run standalone
/// against a manually-started emulator, this is what makes that work.
/// </summary>
public static class CosmosBootstrapper
{
    public static async Task EnsureDatabaseAndContainersAsync(CosmosClient client, CosmosOptions options)
    {
        var database = (await client.CreateDatabaseIfNotExistsAsync(options.Database)).Database;
        await database.CreateContainerIfNotExistsAsync(options.CustomersContainer, "/id");
        await database.CreateContainerIfNotExistsAsync(options.OrdersContainer, "/customerId");
    }
}
