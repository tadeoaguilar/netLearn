using Aspire.Hosting;

// .NET Aspire orchestration.
//
//     dotnet run --project src/TaskManagement.AppHost
//
// Requires Docker: Aspire starts PostgreSQL in a container, wires the
// connection string into the API, and opens a dashboard showing logs, traces
// and metrics across both.
//
// The API itself does NOT require any of this. Run it directly and it uses
// SQLite -- see appsettings.json. Aspire is orchestration, not a dependency,
// and keeping that line clear is the point.

var builder = DistributedApplication.CreateBuilder(args);

// A PostgreSQL container with a persistent volume, so data survives a restart.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var database = postgres.AddDatabase("taskmanagement");

builder.AddProject<Projects.TaskManagement_WebApi>("api")
    .WithReference(database)
    .WaitFor(database)

    // Aspire injects ConnectionStrings__taskmanagement. The API reads
    // ConnectionStrings:Default, so it is mapped across explicitly rather than
    // renaming the setting and coupling the API to its orchestrator.
    // Development so the API uses its local dev token issuer; point
    // Jwt__Authority at a real HTTPS provider to run against one instead.
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("Database__Provider", "postgres")
    .WithEnvironment(context =>
    {
        context.EnvironmentVariables["ConnectionStrings__Default"] =
            database.Resource.ConnectionStringExpression;
    })
    .WithExternalHttpEndpoints();

builder.Build().Run();
