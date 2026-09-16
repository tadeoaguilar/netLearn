using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace CosmosConsistencyAndTransactions.Tests;

/// <summary>
/// Starts the AppHost (and therefore the Cosmos DB emulator) ONCE for the
/// whole test run, via the shared xUnit collection fixture below.
/// </summary>
public class CosmosFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public string DatabaseName { get; } = "cosmostransactions";
    public string CustomersContainerName { get; } = "customers";
    public string OrdersContainerName { get; } = "orders";

    public string ConnectionString { get; private set; } = string.Empty;
    public CosmosClient Client { get; private set; } = null!;

    public Container Customers => Client.GetContainer(DatabaseName, CustomersContainerName);
    public Container Orders => Client.GetContainer(DatabaseName, OrdersContainerName);

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CosmosConsistencyAndTransactions_AppHost>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications
            .WaitForResourceAsync("cosmos", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));

        ConnectionString = await _app.GetConnectionStringAsync("cosmos")
            ?? throw new InvalidOperationException("Cosmos connection string not available.");

        Client = new CosmosClient(ConnectionString, new CosmosClientOptions { ConsistencyLevel = ConsistencyLevel.Session });

        var database = (await Client.CreateDatabaseIfNotExistsAsync(DatabaseName)).Database;
        await database.CreateContainerIfNotExistsAsync(CustomersContainerName, "/id");
        await database.CreateContainerIfNotExistsAsync(OrdersContainerName, "/customerId");
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

[CollectionDefinition("Cosmos emulator")]
public class CosmosCollection : ICollectionFixture<CosmosFixture>
{
}
