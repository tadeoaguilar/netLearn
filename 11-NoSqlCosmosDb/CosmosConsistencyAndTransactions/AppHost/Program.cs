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

var builder = DistributedApplication.CreateBuilder(args);

var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator();

var database = cosmos.AddCosmosDatabase("cosmostransactions");
database.AddContainer("customers", "/id");
database.AddContainer("orders", "/customerId");

builder.AddProject<Projects.CosmosConsistencyAndTransactions_Solution>("app")
    .WithReference(cosmos)
    .WaitFor(cosmos);

builder.Build().Run();
