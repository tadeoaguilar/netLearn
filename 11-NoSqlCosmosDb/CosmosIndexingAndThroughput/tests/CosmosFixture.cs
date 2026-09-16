using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace CosmosIndexingAndThroughput.Tests;

// Starts the Cosmos DB emulator ONCE for the whole test run (via the
// AppHost, same as the module's other test projects) and hands every test
// class a ready CosmosClient + Database. Starting the emulator is slow
// (multi-minute first pull), so this is shared through an xUnit collection
// fixture rather than started per test class.
public class CosmosFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public CosmosClient Client { get; private set; } = null!;

    public Database Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CosmosIndexingAndThroughput_AppHost>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications
            .WaitForResourceAsync("cosmos", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));

        var connectionString = await _app.GetConnectionStringAsync("cosmos")
            ?? throw new InvalidOperationException("cosmos connection string was not available.");

        Client = new CosmosClient(connectionString);
        var databaseResponse = await Client.CreateDatabaseIfNotExistsAsync("cosmosindexing");
        Database = databaseResponse.Database;
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

[CollectionDefinition(nameof(CosmosCollection))]
public class CosmosCollection : ICollectionFixture<CosmosFixture>
{
}
