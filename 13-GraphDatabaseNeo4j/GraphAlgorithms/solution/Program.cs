using GraphAlgorithms.Algorithms;
using GraphAlgorithms.Gds;
using GraphAlgorithms.Results;
using GraphAlgorithms.Seeding;
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

await using var session = driver.AsyncSession();

// --- Part 0: seed the graph -------------------------------------------
await GraphSeeder.SeedAsync(session);
Console.WriteLine("Seeded people, companies, KNOWS, WORKS_AT, and FOLLOWS.\n");

// --- Part 1: confirm GDS is actually loaded -----------------------------
var gdsVersion = await session.ExecuteReadAsync(async tx =>
{
    var cursor = await tx.RunAsync(CypherQueries.GdsVersion());
    var record = await cursor.SingleAsync();
    return Neo4j.Driver.ValueExtensions.As<string>(record["version"]);
});
Console.WriteLine($"Graph Data Science library version: {gdsVersion}\n");

// --- Part 2 & 3: project on FOLLOWS, run PageRank -----------------------
await GraphCatalog.ProjectSocialNetworkAsync(session);
Console.WriteLine($"Projected '{GraphNames.SocialNetwork}' (Person + FOLLOWS, natural orientation).");

var influence = await PageRankRunner.RunAsync(session, GraphNames.SocialNetwork);
var topInfluencers = ResultFormatting.TopInfluencers(influence, count: 5);

Console.WriteLine("\nTop influencers by PageRank:");
foreach (var person in topInfluencers)
{
    Console.WriteLine($"  {person.Name,-8} score={person.Score:F4}");
}

// --- Part 4: project on KNOWS, run Louvain ------------------------------
await GraphCatalog.ProjectFriendGroupsAsync(session);
Console.WriteLine($"\nProjected '{GraphNames.FriendGroups}' (Person + KNOWS, undirected orientation).");

var memberships = await LouvainRunner.RunAsync(session, GraphNames.FriendGroups);
var communities = ResultFormatting.GroupByCommunity(memberships);

Console.WriteLine("\nCommunities found by Louvain:");
foreach (var (communityId, members) in communities)
{
    Console.WriteLine($"  Community {communityId}: {string.Join(", ", members)}");
}

// --- Part 5: re-project on FOLLOWS, run Node Similarity -----------------
// The 'social-network' projection was still valid (we haven't written any
// new Person/FOLLOWS data since Part 2), but re-projecting here makes the
// catalog lifecycle explicit rather than relying on a projection created
// several steps earlier.
await GraphCatalog.ProjectSocialNetworkAsync(session);

var similarities = await NodeSimilarityRunner.RunAsync(session, GraphNames.SocialNetwork);
var topPairs = ResultFormatting.TopSimilarPairs(similarities, count: 5, minSimilarity: 0.0);

Console.WriteLine("\nTop similar pairs (\"people you may know\") by Node Similarity:");
foreach (var pair in topPairs)
{
    Console.WriteLine($"  {pair.PersonA} <-> {pair.PersonB}: {pair.Similarity:F4}");
}

// --- Cleanup: drop everything we projected ------------------------------
await GraphCatalog.DropIfExistsAsync(session, GraphNames.SocialNetwork);
await GraphCatalog.DropIfExistsAsync(session, GraphNames.FriendGroups);
Console.WriteLine("\nDropped all projected graphs from the GDS catalog.");
