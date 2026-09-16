using GraphModeling.Domain;
using GraphModeling.Persistence;
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
Console.WriteLine();

await using var session = driver.AsyncSession();

// ---------------------------------------------------------------------
// Part 1: Nodes and labels
// ---------------------------------------------------------------------
await GraphWriter.CreatePersonAsync(session, new Person("p1", "Alice Anderson"));
await GraphWriter.CreatePersonAsync(session, new Person("p9", "Ingrid Iversen"));
await GraphWriter.CreateCompanyAsync(session, new Company("c1", "Acme Robotics"));
Console.WriteLine("Part 1: created Person nodes (p1, p9) and a Company node (c1).");

// ---------------------------------------------------------------------
// Part 2: Relationships as first-class data
// ---------------------------------------------------------------------
await GraphWriter.CreateWorksAtAsync(session, "p1", "c1", "Engineering Lead", 2018);
var (role, since) = await ReadWorksAtAsync(session, "p1", "c1");
Console.WriteLine($"Part 2: p1 WORKS_AT c1 as '{role}' since {since} -- role/since live on the edge itself.");

// ---------------------------------------------------------------------
// Part 3: Directionality -- FOLLOWS (genuinely directed) vs. KNOWS
// (conceptually symmetric, normalized by convention)
// ---------------------------------------------------------------------
await GraphWriter.CreateFollowsAsync(session, "p1", "p9");
// Passed in "reverse" order on purpose -- KnowsConvention normalizes it.
await GraphWriter.CreateKnowsAsync(session, "p9", "p1", 2015);

var knowsDirection = await ReadKnowsDirectionAsync(session, "p1", "p9");
Console.WriteLine("Part 3: p1 FOLLOWS p9 (one-directional, as passed in).");
Console.WriteLine($"        KNOWS(p9, p1) was requested, but is stored as {knowsDirection} -- the lexicographically smaller id is always the source.");

// ---------------------------------------------------------------------
// Part 4: Uniqueness constraints and indexes
// ---------------------------------------------------------------------
await GraphSchema.EnsureConstraintsAndIndexesAsync(session);
Console.WriteLine("Part 4: ensured Person.id/Company.id uniqueness constraints and the Person.name index.");

// ---------------------------------------------------------------------
// Part 5: The shared seed dataset (idempotent -- safe to run repeatedly)
// ---------------------------------------------------------------------
await GraphSeeder.SeedAsync(session);
var personCount = await CountAsync(session, "MATCH (p:Person) RETURN count(p) AS c");
var companyCount = await CountAsync(session, "MATCH (c:Company) RETURN count(c) AS c");
var worksAtCount = await CountAsync(session, "MATCH ()-[r:WORKS_AT]->() RETURN count(r) AS c");
var knowsCount = await CountAsync(session, "MATCH ()-[r:KNOWS]->() RETURN count(r) AS c");
var followsCount = await CountAsync(session, "MATCH ()-[r:FOLLOWS]->() RETURN count(r) AS c");

Console.WriteLine("Part 5: seeded the shared domain --");
Console.WriteLine($"        {personCount} Person nodes, {companyCount} Company nodes");
Console.WriteLine($"        {worksAtCount} WORKS_AT, {knowsCount} KNOWS, {followsCount} FOLLOWS relationships");
Console.WriteLine();
Console.WriteLine("Run this again -- the counts above will not change. The seed is idempotent.");

static async Task<(string Role, long Since)> ReadWorksAtAsync(IAsyncSession session, string personId, string companyId)
{
    return await session.ExecuteReadAsync(async tx =>
    {
        var cursor = await tx.RunAsync(
            """
            MATCH (p:Person {id: $personId})-[r:WORKS_AT]->(c:Company {id: $companyId})
            RETURN r.role AS role, r.since AS since
            """,
            new { personId, companyId });
        var record = await cursor.SingleAsync();
        return (
            Neo4j.Driver.ValueExtensions.As<string>(record["role"]),
            Neo4j.Driver.ValueExtensions.As<long>(record["since"]));
    });
}

static async Task<string> ReadKnowsDirectionAsync(IAsyncSession session, string idA, string idB)
{
    return await session.ExecuteReadAsync(async tx =>
    {
        var cursor = await tx.RunAsync(
            """
            MATCH (a:Person)-[:KNOWS]->(b:Person)
            WHERE a.id IN [$idA, $idB] AND b.id IN [$idA, $idB]
            RETURN a.id AS fromId, b.id AS toId
            """,
            new { idA, idB });
        var record = await cursor.SingleAsync();
        return $"{Neo4j.Driver.ValueExtensions.As<string>(record["fromId"])} -> {Neo4j.Driver.ValueExtensions.As<string>(record["toId"])}";
    });
}

static async Task<long> CountAsync(IAsyncSession session, string query)
{
    return await session.ExecuteReadAsync(async tx =>
    {
        var cursor = await tx.RunAsync(query);
        var record = await cursor.SingleAsync();
        return Neo4j.Driver.ValueExtensions.As<long>(record["c"]);
    });
}
