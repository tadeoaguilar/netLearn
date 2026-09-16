using GraphTraversals;
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
Console.WriteLine("Seeded the professional-network sample graph (idempotent -- safe to run again).");

var queries = new TraversalQueries(driver);

Console.WriteLine();
Console.WriteLine("=== Part 1: shortestPath (erin -> ivan) ===");
var shortest = await queries.ShortestPathAsync("erin", "ivan");
Console.WriteLine(string.Join(" -> ", shortest));

Console.WriteLine();
Console.WriteLine("=== Part 2: allShortestPaths (carol <-> grace) ===");
var allShortest = await queries.AllShortestPathsAsync("carol", "grace");
foreach (var path in allShortest)
{
    Console.WriteLine(string.Join(" -> ", path));
}

Console.WriteLine();
Console.WriteLine("=== Part 3: within 3 KNOWS hops of alice ===");
var withinHops = await queries.WithinHopsAsync("alice", maxHops: 3);
Console.WriteLine(string.Join(", ", withinHops.OrderBy(id => id)));
Console.WriteLine("(peggy is 5 hops from alice and should NOT appear above)");

Console.WriteLine();
Console.WriteLine("=== Part 4: mutual KNOWS connections (bob <-> judy) ===");
var mutual = await queries.MutualConnectionsAsync("bob", "judy");
Console.WriteLine(string.Join(", ", mutual));

Console.WriteLine();
Console.WriteLine("=== Part 5: degrees of separation from alice (up to 5 hops) ===");
var degrees = await queries.DegreesOfSeparationAsync("alice", maxHops: 5);
foreach (var (hops, ids) in degrees.OrderBy(kv => kv.Key))
{
    Console.WriteLine($"{hops} hop(s): {string.Join(", ", ids)}");
}
