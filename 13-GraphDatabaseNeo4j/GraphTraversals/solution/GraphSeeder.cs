using Neo4j.Driver;

namespace GraphTraversals;

/// <summary>
/// Builds the professional-network sample graph this module's exercises run
/// against. Every write uses MERGE, so calling <see cref="SeedAsync"/>
/// against an already-seeded database is a no-op (aside from re-applying the
/// same property values) rather than creating duplicate nodes/relationships.
///
/// The shape of the graph is deliberate, not random -- see the comments
/// below and EXERCISE.md for what each part is designed to demonstrate:
///
///   - Alice and Bob are hubs: five KNOWS relationships each.
///   - Erin and Ivan know nobody in common and are three KNOWS hops apart,
///     via exactly one shortest path (Erin-Alice-Bob-Ivan). Good for
///     shortestPath().
///   - Carol and Grace are two hops apart by exactly two equally-short
///     paths (via Bob, and via Judy). Good for allShortestPaths().
///   - Mallory/Niaj/Olivia form a small "island" cluster, hanging off the
///     rest of the graph by a single bridge edge (Judy-Mallory). Peggy
///     hangs even further out, off Olivia alone.
///   - Starting from Alice, Peggy is exactly five KNOWS hops away -- far
///     enough that a *1..3 bounded traversal must NOT reach her.
/// </summary>
public static class GraphSeeder
{
    public static async Task SeedAsync(IDriver driver, CancellationToken cancellationToken = default)
    {
        await using var session = driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "CREATE CONSTRAINT person_id_unique IF NOT EXISTS FOR (p:Person) REQUIRE p.id IS UNIQUE");
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "CREATE CONSTRAINT company_id_unique IF NOT EXISTS FOR (c:Company) REQUIRE c.id IS UNIQUE");
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $people AS person
                MERGE (p:Person {id: person.id})
                SET p.name = person.name
                """,
                new { people = People });
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $companies AS company
                MERGE (c:Company {id: company.id})
                SET c.name = company.name
                """,
                new { companies = Companies });
            await cursor.ConsumeAsync();
        });

        // KNOWS is modeled as a single directed relationship (the "from"
        // side is just where it happens to be created) but, per this
        // module's convention, every query matches it undirected with
        // -[:KNOWS]- since "knowing someone" is conceptually symmetric.
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $edges AS edge
                MATCH (a:Person {id: edge.a}), (b:Person {id: edge.b})
                MERGE (a)-[r:KNOWS]->(b)
                SET r.since = edge.since
                """,
                new { edges = KnowsEdges });
            await cursor.ConsumeAsync();
        });

        // FOLLOWS is genuinely directed and asymmetric: Alice is followed
        // by Carol, Grace, and Heidi, but Alice's own follow of Bob is not
        // reciprocated. Direction matters for this relationship, unlike
        // KNOWS.
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $edges AS edge
                MATCH (a:Person {id: edge.from}), (b:Person {id: edge.to})
                MERGE (a)-[r:FOLLOWS]->(b)
                """,
                new { edges = FollowsEdges });
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $edges AS edge
                MATCH (p:Person {id: edge.personId}), (c:Company {id: edge.companyId})
                MERGE (p)-[r:WORKS_AT]->(c)
                SET r.role = edge.role, r.since = edge.since
                """,
                new { edges = WorksAtEdges });
            await cursor.ConsumeAsync();
        });
    }

    public static readonly IReadOnlyList<object> People =
    [
        new { id = "alice", name = "Alice Nakamura" },
        new { id = "bob", name = "Bob Okafor" },
        new { id = "carol", name = "Carol Jimenez" },
        new { id = "dave", name = "Dave Whitfield" },
        new { id = "erin", name = "Erin Kowalski" },
        new { id = "frank", name = "Frank Delgado" },
        new { id = "grace", name = "Grace Lindqvist" },
        new { id = "heidi", name = "Heidi Mercer" },
        new { id = "ivan", name = "Ivan Petrov" },
        new { id = "judy", name = "Judy Alvarez" },
        new { id = "mallory", name = "Mallory Chen" },
        new { id = "niaj", name = "Niaj Rahman" },
        new { id = "olivia", name = "Olivia Ferreira" },
        new { id = "peggy", name = "Peggy Sato" },
    ];

    public static readonly IReadOnlyList<object> Companies =
    [
        new { id = "techcorp", name = "TechCorp" },
        new { id = "datasystems", name = "DataSystems" },
        new { id = "startupx", name = "StartupX" },
    ];

    // Undirected-in-spirit KNOWS graph. Distances referenced throughout
    // this file and in the tests are computed by BFS over this edge list
    // treated as undirected.
    public static readonly IReadOnlyList<object> KnowsEdges =
    [
        new { a = "alice", b = "bob", since = 2015 },
        new { a = "alice", b = "carol", since = 2016 },
        new { a = "alice", b = "dave", since = 2017 },
        new { a = "alice", b = "erin", since = 2018 },
        new { a = "alice", b = "frank", since = 2018 },
        new { a = "bob", b = "carol", since = 2016 },
        new { a = "bob", b = "grace", since = 2019 },
        new { a = "bob", b = "heidi", since = 2020 },
        new { a = "bob", b = "ivan", since = 2021 },
        new { a = "carol", b = "judy", since = 2019 },
        new { a = "dave", b = "judy", since = 2020 },
        new { a = "grace", b = "judy", since = 2021 },
        new { a = "erin", b = "frank", since = 2017 },
        new { a = "judy", b = "mallory", since = 2022 }, // the one bridge edge into the island
        new { a = "mallory", b = "niaj", since = 2022 },
        new { a = "mallory", b = "olivia", since = 2023 },
        new { a = "niaj", b = "olivia", since = 2023 },
        new { a = "olivia", b = "peggy", since = 2024 },
    ];

    public static readonly IReadOnlyList<object> FollowsEdges =
    [
        new { from = "alice", to = "bob" }, // one-directional: bob does not follow alice back
        new { from = "alice", to = "carol" },
        new { from = "carol", to = "alice" },
        new { from = "grace", to = "alice" },
        new { from = "heidi", to = "alice" },
        new { from = "ivan", to = "bob" },
        new { from = "judy", to = "bob" },
        new { from = "dave", to = "judy" },
        new { from = "erin", to = "frank" },
        new { from = "mallory", to = "judy" },
        new { from = "niaj", to = "mallory" },
        new { from = "olivia", to = "mallory" },
        new { from = "peggy", to = "olivia" },
    ];

    public static readonly IReadOnlyList<object> WorksAtEdges =
    [
        new { personId = "alice", companyId = "techcorp", role = "Engineering Manager", since = 2015 },
        new { personId = "bob", companyId = "techcorp", role = "Staff Engineer", since = 2016 },
        new { personId = "carol", companyId = "techcorp", role = "Product Manager", since = 2018 },
        new { personId = "dave", companyId = "datasystems", role = "Data Engineer", since = 2017 },
        new { personId = "erin", companyId = "datasystems", role = "Analyst", since = 2019 },
        new { personId = "mallory", companyId = "startupx", role = "Founder", since = 2022 },
        new { personId = "niaj", companyId = "startupx", role = "Backend Engineer", since = 2023 },
    ];
}
