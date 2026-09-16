using CosmosIndexingAndThroughput.Data;
using CosmosIndexingAndThroughput.Indexing;
using CosmosIndexingAndThroughput.Metrics;
using CosmosIndexingAndThroughput.Retry;
using CosmosIndexingAndThroughput.Throughput;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("cosmos")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:cosmos.");

builder.Services.AddSingleton(_ => new CosmosClient(connectionString));

var host = builder.Build();
var cosmosClient = host.Services.GetRequiredService<CosmosClient>();

const string DatabaseName = "cosmosindexing";

var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);
var database = databaseResponse.Database;
var customersContainer = database.GetContainer("customers");
var ordersContainer = database.GetContainer("orders");

Console.WriteLine($"CosmosClient configured for endpoint: {cosmosClient.Endpoint}");
Console.WriteLine("Seeding sample data (idempotent -- skips containers that already have enough documents)...");
var customers = await DataSeeder.SeedCustomersAsync(customersContainer);
await DataSeeder.SeedOrdersAsync(ordersContainer, customers);
Console.WriteLine($"Ready: {customers.Count} customers seeded/loaded.");
Console.WriteLine();

await IndexingWalkthrough.RunPart1DefaultIndexingAsync(ordersContainer, customers);
await IndexingWalkthrough.RunPart2CustomIndexingPolicyAsync(database, ordersContainer, customers);
await RequestChargeDemo.RunAsync(ordersContainer, customers);
await ThroughputWalkthrough.RunAsync(database);
await RetryWalkthrough.RunAsync(ordersContainer, customers, connectionString);

Console.WriteLine("Done -- see EXERCISE.md to work through each part yourself in the workspace project.");
