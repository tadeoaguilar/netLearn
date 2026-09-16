using Aspire.Hosting;

// .NET Aspire orchestration.
//
//     dotnet run --project AppHost
//
// Requires Docker: Aspire starts the Cosmos DB Linux emulator in a
// container (a heavier image than Postgres -- expect a multi-minute first
// start), waits for it to be ready, and wires its connection string into
// the app.
//
// The app itself does NOT require any of this. Run `solution` directly
// against a manually-started emulator (see the module README) and it works
// unmodified -- Aspire is orchestration, not a dependency.
//
// Note: container-level indexing policy and throughput (manual vs.
// autoscale RU/s) are NOT configured here. The Aspire Azure CosmosDB
// hosting API's AddContainer only exposes a name and a partition key path --
// it has no overload for an indexing policy or a throughput profile. Those
// are configured from the client SDK side instead, in solution/Program.cs
// (see Indexing/IndexingPolicyFactory.cs and Throughput/ThroughputWalkthrough.cs).

var builder = DistributedApplication.CreateBuilder(args);

var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator();

var database = cosmos.AddCosmosDatabase("cosmosindexing");
database.AddContainer("customers", "/id");
database.AddContainer("orders", "/customerId");

builder.AddProject<Projects.CosmosIndexingAndThroughput_Solution>("app")
    .WithReference(cosmos)
    .WaitFor(cosmos);

builder.Build().Run();
