using GraphQuerying.Queries;
using GraphQuerying.Seed;
using Microsoft.Extensions.Configuration;
using Neo4j.Driver;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var uri = configuration["Neo4j:Uri"] ?? throw new InvalidOperationException("Missing Neo4j:Uri.");
var username = configuration["Neo4j:Username"] ?? throw new InvalidOperationException("Missing Neo4j:Username.");
var password = configuration["Neo4j:Password"] ?? throw new InvalidOperationException("Missing Neo4j:Password.");

await using var driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
await driver.VerifyConnectivityAsync();

Console.WriteLine($"Connected to Neo4j at {uri}");

await GraphSeeder.SeedAsync(driver);
Console.WriteLine("Seed data ensured (idempotent): " +
    $"{GraphSeeder.People.Count} people, {GraphSeeder.Companies.Count} companies, " +
    $"{GraphSeeder.WorksAtEdges.Count} WORKS_AT, {GraphSeeder.KnowsEdges.Count} KNOWS, " +
    $"{GraphSeeder.FollowsEdges.Count} FOLLOWS.");

await DriverBasicsDemo.RunAsync(driver);
await ParameterizedQueriesDemo.RunAsync(driver);
await PatternMatchingDemo.RunAsync(driver);
await VariableLengthDemo.RunAsync(driver);
await AggregationAndPaginationDemo.RunAsync(driver);
