using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace CosmosChangeFeed.Tests;

/// <summary>
/// Starts the Aspire AppHost -- and with it, the Cosmos DB emulator and the
/// "customers", "orders", "customerordersummary" and "leases" containers --
/// exactly once for the whole test run, then hands every test a
/// <see cref="CosmosClient"/> pointed at it.
///
/// The emulator is the single slowest part of this module's test suite:
/// first start of the (heavier, Linux-based) Cosmos emulator image can take
/// several minutes, well beyond the 3-minute wait below on a cold Docker
/// cache. Sharing one instance across every test in the collection -- via
/// <see cref="CosmosCollection"/> -- means that cost is paid once, not once
/// per test class.
/// </summary>
public class CosmosEmulatorFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public CosmosClient CosmosClient { get; private set; } = null!;

    public Database Database { get; private set; } = null!;

    public Container OrdersContainer { get; private set; } = null!;

    public Container SummaryContainer { get; private set; } = null!;

    public Container LeaseContainer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CosmosChangeFeed_AppHost>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications
            .WaitForResourceAsync("cosmos", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));

        var connectionString = await _app.GetConnectionStringAsync("cosmos")
            ?? throw new InvalidOperationException("The cosmos resource did not produce a connection string.");

        CosmosClient = new CosmosClient(connectionString);
        Database = CosmosClient.GetDatabase(Program.DatabaseName);
        OrdersContainer = Database.GetContainer(Program.OrdersContainerName);
        SummaryContainer = Database.GetContainer(Program.SummaryContainerName);
        LeaseContainer = Database.GetContainer(Program.LeasesContainerName);
    }

    public async Task DisposeAsync()
    {
        CosmosClient?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// Starts a change feed processor against <see cref="OrdersContainer"/>
    /// using a fresh <see cref="OrderChangeHandler"/> writing into
    /// <see cref="SummaryContainer"/>. Each test that starts its own
    /// processor should use a distinct <paramref name="instanceName"/> (and
    /// usually a distinct processor name, unless the test is deliberately
    /// exercising lease hand-off between instances) so tests don't fight
    /// over the same lease documents.
    /// </summary>
    public ChangeFeedProcessor BuildProcessor(string processorName, string instanceName)
    {
        var handler = new OrderChangeHandler(OrdersContainer, SummaryContainer);

        return OrdersContainer
            .GetChangeFeedProcessorBuilder<Order>(processorName, handler.HandleChangesAsync)
            .WithInstanceName(instanceName)
            .WithLeaseContainer(LeaseContainer)
            .Build();
    }
}

/// <summary>
/// Declares the shared xUnit collection so every test class below reuses
/// one <see cref="CosmosEmulatorFixture"/> (and hence one emulator) instead
/// of starting a new one per class.
/// </summary>
[CollectionDefinition("Cosmos emulator")]
public class CosmosCollection : ICollectionFixture<CosmosEmulatorFixture>;
