using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CosmosChangeFeed;

public static class Program
{
    public const string DatabaseName = "cosmoschangefeed";
    public const string OrdersContainerName = "orders";
    public const string SummaryContainerName = "customerordersummary";
    public const string LeasesContainerName = "leases";

    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("cosmos")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:cosmos.");

        builder.Services.AddSingleton(_ => new CosmosClient(connectionString));

        var host = builder.Build();
        var cosmosClient = host.Services.GetRequiredService<CosmosClient>();

        Console.WriteLine($"CosmosClient configured for endpoint: {cosmosClient.Endpoint}");

        var database = cosmosClient.GetDatabase(DatabaseName);
        var ordersContainer = database.GetContainer(OrdersContainerName);
        var summaryContainer = database.GetContainer(SummaryContainerName);
        var leaseContainer = database.GetContainer(LeasesContainerName);

        var handler = new OrderChangeHandler(ordersContainer, summaryContainer);

        // "orderProcessor" is the processor NAME -- it groups a set of
        // instances (see WithInstanceName below) that cooperatively split
        // the container's partitions between them, and it is baked into
        // the lease documents this processor writes to leaseContainer.
        // Changing the processor name starts a brand new lease set, which
        // means reprocessing the container's history from the beginning.
        var processor = ordersContainer
            .GetChangeFeedProcessorBuilder<Order>("orderProcessor", handler.HandleChangesAsync)
            .WithInstanceName($"host-{Environment.MachineName}-{Environment.ProcessId}")
            .WithLeaseContainer(leaseContainer)
            .Build();

        Console.WriteLine("Starting change feed processor...");
        await processor.StartAsync();

        // Give the processor a moment to acquire its leases before writing
        // -- on a cold start, lease acquisition and the first estimation
        // pass take a little real time. This is exactly why change feed
        // exercises feel slower than the module's other projects: there is
        // no way to make "the processor is now watching" instantaneous.
        await Task.Delay(TimeSpan.FromSeconds(2));

        await SeedOrdersAsync(ordersContainer);

        Console.WriteLine("Polling customerordersummary for the materialized totals...");
        var summary = await PollForSummaryAsync(summaryContainer, customerId: "customer-1", expectedOrders: 2, timeout: TimeSpan.FromSeconds(30));

        if (summary is not null)
        {
            Console.WriteLine($"customer-1 summary -> TotalOrders: {summary.TotalOrders}, TotalSpent: {summary.TotalSpent:C}, LastOrderDate: {summary.LastOrderDate}");
        }
        else
        {
            Console.WriteLine("Timed out waiting for the summary to materialize. Is the change feed processor running against the right containers?");
        }

        await processor.StopAsync();
        Console.WriteLine("Change feed processor stopped.");
    }

    private static async Task SeedOrdersAsync(Container ordersContainer)
    {
        var firstOrder = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = "customer-1",
            OrderDate = DateTimeOffset.UtcNow,
            Status = "Placed",
            OrderLines = [new OrderLine { ProductId = "p1", ProductName = "Keyboard", Quantity = 1, UnitPrice = 49.99m }],
            TotalAmount = 49.99m,
        };

        var secondOrder = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = "customer-1",
            OrderDate = DateTimeOffset.UtcNow,
            Status = "Placed",
            OrderLines = [new OrderLine { ProductId = "p2", ProductName = "Mouse", Quantity = 2, UnitPrice = 19.99m }],
            TotalAmount = 39.98m,
        };

        Console.WriteLine("Writing two orders for customer-1...");
        await ordersContainer.CreateItemAsync(firstOrder, new PartitionKey(firstOrder.CustomerId));
        await ordersContainer.CreateItemAsync(secondOrder, new PartitionKey(secondOrder.CustomerId));
    }

    /// <summary>
    /// Change feed processing is asynchronous and eventual: writing an
    /// Order does not block until the summary is updated. Polling with a
    /// timeout -- rather than a single fixed delay -- is the only reliable
    /// way to observe the result, because how long it takes depends on the
    /// processor's poll interval and how recently it last ran.
    /// </summary>
    public static async Task<CustomerOrderSummary?> PollForSummaryAsync(
        Container summaryContainer,
        string customerId,
        int expectedOrders,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var response = await summaryContainer.ReadItemAsync<CustomerOrderSummary>(
                    customerId,
                    new PartitionKey(customerId),
                    cancellationToken: cts.Token);

                if (response.Resource.TotalOrders >= expectedOrders)
                {
                    return response.Resource;
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // The processor hasn't written the summary document yet.
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return null;
    }
}
