using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using CosmosModeling.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace CosmosModeling.Tests;

/// <summary>
/// Boots the CosmosModeling AppHost -- Cosmos DB emulator included -- ONCE for the whole
/// test run and shares it across every test class in <see cref="CosmosModelingCollection"/>.
/// The emulator's cold start is measured in minutes, not seconds, so a fresh AppHost per
/// test (the usual "clean database per test" pattern) is not viable here. This mirrors
/// <c>PostgresFixture</c> in
/// <c>10-EntityFrameworkCore/EfCoreQuerying/tests/PostgresFixture.cs</c>, just against a
/// much slower-starting dependency.
/// </summary>
public sealed class CosmosModelingFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public CosmosClient Client { get; private set; } = null!;

    public Container CustomersContainer { get; private set; } = null!;

    public Container OrdersContainer { get; private set; } = null!;

    public Container HighVolumeOrdersContainer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CosmosModeling_AppHost>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications
            .WaitForResourceAsync("cosmos", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));

        var connectionString = await _app.GetConnectionStringAsync("cosmos")
            ?? throw new InvalidOperationException("Cosmos connection string was not available after the emulator started.");

        Client = new CosmosClient(connectionString);

        var database = await CosmosInitializer.EnsureDatabaseAsync(Client);
        CustomersContainer = await CosmosInitializer.EnsureCustomersContainerAsync(database);
        OrdersContainer = await CosmosInitializer.EnsureOrdersContainerAsync(database);
        HighVolumeOrdersContainer = await CosmosInitializer.EnsureHighVolumeOrdersContainerAsync(database);
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

/// <summary>
/// Every test class that needs the shared emulator implements this collection so xUnit
/// starts it once and runs those tests serially against it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CosmosModelingCollection : ICollectionFixture<CosmosModelingFixture>
{
    public const string Name = "CosmosModeling";
}
