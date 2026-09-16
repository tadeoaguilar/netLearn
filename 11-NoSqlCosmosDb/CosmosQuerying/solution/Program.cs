using CosmosQuerying.Demos;
using CosmosQuerying.Persistence;
using CosmosQuerying.Seed;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connectionString = configuration.GetConnectionString("cosmos")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:cosmos in appsettings.json.");

using var cosmosClient = new CosmosClient(connectionString, CosmosSerialization.ClientOptions);

Console.WriteLine("Ensuring database and containers exist...");
var database = await CosmosInitializer.EnsureDatabaseAsync(cosmosClient);
var customers = database.GetContainer(CosmosInitializer.CustomersContainer);
var orders = database.GetContainer(CosmosInitializer.OrdersContainer);

Console.WriteLine("Seeding data (no-op if already seeded)...");
await OrderSeeder.SeedAsync(customers, orders);

var parts = new Dictionary<string, Func<Container, Container, Task>>
{
    ["1"] = (_, o) => Part1Linq.RunAsync(o),
    ["2"] = (_, o) => Part2SqlApi.RunAsync(o),
    ["3"] = (_, o) => Part3Partitioning.RunAsync(o, "cust-001"),
    ["4"] = (_, o) => Part4Pagination.RunAsync(o),
    ["5"] = (_, o) => Part5Projections.RunAsync(o),
};

var selection = args.Length > 0 ? args[0] : "all";

if (selection == "all")
{
    foreach (var (_, run) in parts.OrderBy(p => int.Parse(p.Key)))
    {
        await run(customers, orders);
    }
}
else if (parts.TryGetValue(selection, out var runOne))
{
    await runOne(customers, orders);
}
else
{
    Console.WriteLine($"Unknown part '{selection}'. Usage: dotnet run -- [1-5|all]");
}
