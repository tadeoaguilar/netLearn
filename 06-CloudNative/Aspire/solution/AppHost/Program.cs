using Aspire.Hosting;

// .NET Aspire orchestration -- the subject of this exercise, not just its
// plumbing. See EXERCISE.md Part A for a guided tour of every feature used
// below, and Part B for what happens when you point `azd` at this project.
//
//     dotnet run --project AppHost
//
// Requires Docker: Aspire starts Postgres and Redis in containers, wires
// their connection info into CatalogApi, and opens a dashboard showing
// logs, traces and metrics across all four resources.

var builder = DistributedApplication.CreateBuilder(args);

// A parameter resource: a configurable value that flows from the AppHost
// into a project's environment, without hardcoding it in either project.
// Override it with `dotnet user-secrets set Parameters:seed-product-count 25`
// or an environment variable -- see EXERCISE.md Part A.6.
var seedProductCount = builder.AddParameter("seed-product-count", "10");

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var catalogDb = postgres.AddDatabase("catalogdb");

var cache = builder.AddRedis("cache")
    .WithDataVolume();

var catalogApi = builder.AddProject<Projects.CatalogApi_Solution>("catalogapi")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(cache)
    .WaitFor(cache)
    .WithEnvironment("SeedProductCount", seedProductCount)
    // Two instances of the same service -- watch the dashboard's resource
    // graph show both, and Storefront's requests land on either one.
    .WithReplicas(2);

builder.AddProject<Projects.Storefront_Solution>("storefront")
    // WithReference is what makes "http://catalogapi" resolvable via
    // service discovery inside Storefront -- without this line, the same
    // HttpClient code in Storefront/Program.cs would fail to resolve the
    // address at all.
    .WithReference(catalogApi)
    .WaitFor(catalogApi)
    .WithExternalHttpEndpoints();

builder.Build().Run();
