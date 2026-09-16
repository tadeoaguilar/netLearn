using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using CosmosQuerying.Persistence;
using CosmosQuerying.Seed;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace CosmosQuerying.Tests;

/// <summary>
/// Starts the Cosmos DB emulator ONCE per test run via the AppHost (the
/// emulator is slow to boot -- minutes, not seconds), ensures the database
/// and containers exist, seeds the deterministic order catalog once, and
/// hands every test the same two <see cref="Container"/> references against
/// that one emulator instance. Modeled on
/// 10-EntityFrameworkCore/EfCoreQuerying/tests/PostgresFixture.cs, adapted
/// for the Cosmos SDK + Aspire testing instead of EF Core + Testcontainers.
/// </summary>
public sealed class CosmosFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private CosmosClient? _client;

    public Container Customers { get; private set; } = null!;
    public Container Orders { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CosmosQuerying_AppHost>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications
            .WaitForResourceAsync("cosmos", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));

        var connectionString = await _app.GetConnectionStringAsync("cosmos")
            ?? throw new InvalidOperationException("Cosmos emulator did not publish a connection string.");

        _client = new CosmosClient(connectionString, CosmosSerialization.ClientOptions);

        var database = await CosmosInitializer.EnsureDatabaseAsync(_client);
        Customers = database.GetContainer(CosmosInitializer.CustomersContainer);
        Orders = database.GetContainer(CosmosInitializer.OrdersContainer);

        await OrderSeeder.SeedAsync(Customers, Orders);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

/// <summary>
/// Every test class implements this collection so they all share one
/// emulator (fast) and xunit runs them serially (safe -- nothing here
/// mutates the shared data, but the emulator itself is a limited resource
/// under test in some of the RU-charge assertions).
/// </summary>
[CollectionDefinition(Name)]
public sealed class CosmosCollection : ICollectionFixture<CosmosFixture>
{
    public const string Name = "Cosmos";
}
