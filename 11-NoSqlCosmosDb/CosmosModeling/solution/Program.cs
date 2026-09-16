using CosmosModeling.Demos;
using CosmosModeling.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connectionString = configuration.GetConnectionString("cosmos")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:cosmos.");

using var client = new CosmosClient(connectionString);

Console.WriteLine("Ensuring database and containers exist (idempotent -- Aspire already");
Console.WriteLine("created them when this runs via AppHost; this also makes standalone runs");
Console.WriteLine("against a manually-started emulator work unmodified)...\n");

var database = await CosmosInitializer.EnsureDatabaseAsync(client);
var customers = await CosmosInitializer.EnsureCustomersContainerAsync(database);
var orders = await CosmosInitializer.EnsureOrdersContainerAsync(database);
var highVolumeOrders = await CosmosInitializer.EnsureHighVolumeOrdersContainerAsync(database);

var parts = new Dictionary<string, Func<Task>>
{
    ["1"] = () => Part1CustomerDocument.RunAsync(customers),
    ["2"] = () => Part2OrderDocument.RunAsync(orders),
    ["3"] = () => Part3EmbeddingVsReferencing.RunAsync(orders),
    ["4"] = () => Part4SchemaEvolution.RunAsync(orders),
    ["5"] = () => Part5SyntheticPartitionKey.RunAsync(highVolumeOrders),
};

var selection = args.Length > 0 ? args[0] : "all";

if (selection == "all")
{
    foreach (var (_, run) in parts.OrderBy(p => int.Parse(p.Key)))
    {
        await run();
    }
}
else if (parts.TryGetValue(selection, out var runOne))
{
    await runOne();
}
else
{
    Console.WriteLine($"Unknown part '{selection}'. Usage: dotnet run -- [1-5|all]");
}
