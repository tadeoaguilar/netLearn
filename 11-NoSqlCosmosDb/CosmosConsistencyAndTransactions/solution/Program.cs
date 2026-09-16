using CosmosConsistencyAndTransactions;
using CosmosConsistencyAndTransactions.Demos;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("cosmos")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:cosmos.");

var cosmosOptions = builder.Configuration.GetSection("Cosmos").Get<CosmosOptions>() ?? new CosmosOptions();

if (!Enum.TryParse<ConsistencyLevel>(cosmosOptions.ConsistencyLevel, ignoreCase: true, out var configuredLevel))
{
    configuredLevel = ConsistencyLevel.Session;
}

builder.Services.AddSingleton(cosmosOptions);
builder.Services.AddSingleton(_ => new CosmosClient(connectionString, new CosmosClientOptions
{
    ConsistencyLevel = configuredLevel
}));

var host = builder.Build();

var client = host.Services.GetRequiredService<CosmosClient>();
var options = host.Services.GetRequiredService<CosmosOptions>();

await CosmosBootstrapper.EnsureDatabaseAndContainersAsync(client, options);

var database = client.GetDatabase(options.Database);
var customers = database.GetContainer(options.CustomersContainer);
var orders = database.GetContainer(options.OrdersContainer);

var part = args.FirstOrDefault() ?? "all";

async Task RunPartAsync(string label, Func<Task> demo)
{
    Console.WriteLine();
    Console.WriteLine($"==== {label} ====");
    await demo();
}

switch (part)
{
    case "1":
        await RunPartAsync("Part 1: Consistency levels", () => ConsistencyLevelDemo.RunAsync(client, connectionString, customers));
        break;
    case "2":
        await RunPartAsync("Part 2: Per-request overrides and session tokens", () => SessionTokenDemo.RunAsync(connectionString, customers));
        break;
    case "3":
        await RunPartAsync("Part 3: ETag optimistic concurrency", () => OptimisticConcurrencyDemo.RunAsync(customers));
        break;
    case "4":
        await RunPartAsync("Part 4: TransactionalBatch", () => TransactionalBatchDemo.RunAsync(orders));
        break;
    case "5":
        await RunPartAsync("Part 5: When this breaks -- cross-partition atomicity", () => CrossPartitionLimitationsDemo.RunAsync(orders));
        break;
    case "all":
        await RunPartAsync("Part 1: Consistency levels", () => ConsistencyLevelDemo.RunAsync(client, connectionString, customers));
        await RunPartAsync("Part 2: Per-request overrides and session tokens", () => SessionTokenDemo.RunAsync(connectionString, customers));
        await RunPartAsync("Part 3: ETag optimistic concurrency", () => OptimisticConcurrencyDemo.RunAsync(customers));
        await RunPartAsync("Part 4: TransactionalBatch", () => TransactionalBatchDemo.RunAsync(orders));
        await RunPartAsync("Part 5: When this breaks -- cross-partition atomicity", () => CrossPartitionLimitationsDemo.RunAsync(orders));
        break;
    default:
        Console.WriteLine($"Unknown part '{part}'. Use 1-5 or 'all'.");
        break;
}
