using Neo4j.Driver;

namespace GraphAlgorithms.Seeding;

/// <summary>
/// Seeds a small professional-network graph shaped specifically so the GDS
/// algorithms in this project have something meaningful to find:
///
/// - Alice has far more incoming FOLLOWS than anyone else, and her
///   followers include people who are themselves followed -- a clear
///   "most influential" answer for PageRank.
/// - KNOWS relationships form three tight, near-complete clusters (Alice's
///   group, Eve's group, Ivan's group) joined only by two bridging edges,
///   so Louvain should recover exactly those three communities.
/// - Bob and Carol follow an identical set of people (Alice, Frank,
///   Grace), so their Node Similarity score should be the highest in the
///   graph, while Bob and Mallory share no followed accounts at all, so
///   their score should be the lowest.
///
/// Every write here uses MERGE, so running the seed twice leaves the graph
/// exactly as it was after the first run -- no duplicate nodes or edges.
/// </summary>
public static class GraphSeeder
{
    public static async Task EnsureConstraintsAsync(IAsyncSession session)
    {
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
    }

    public static async Task SeedAsync(IAsyncSession session)
    {
        await EnsureConstraintsAsync(session);

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

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $worksAt AS rel
                MATCH (p:Person {id: rel.personId})
                MATCH (c:Company {id: rel.companyId})
                MERGE (p)-[r:WORKS_AT]->(c)
                SET r.role = rel.role, r.since = rel.since
                """,
                new { worksAt = WorksAt });
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $knows AS rel
                MATCH (a:Person {id: rel.fromId})
                MATCH (b:Person {id: rel.toId})
                MERGE (a)-[r:KNOWS]-(b)
                SET r.since = rel.since
                """,
                new { knows = Knows });
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                UNWIND $follows AS rel
                MATCH (a:Person {id: rel.fromId})
                MATCH (b:Person {id: rel.toId})
                MERGE (a)-[:FOLLOWS]->(b)
                """,
                new { follows = Follows });
            await cursor.ConsumeAsync();
        });
    }

    private static readonly IReadOnlyList<object> People = new List<object>
    {
        new Dictionary<string, object> { ["id"] = "p1", ["name"] = "Alice" },
        new Dictionary<string, object> { ["id"] = "p2", ["name"] = "Bob" },
        new Dictionary<string, object> { ["id"] = "p3", ["name"] = "Carol" },
        new Dictionary<string, object> { ["id"] = "p4", ["name"] = "Dave" },
        new Dictionary<string, object> { ["id"] = "p5", ["name"] = "Eve" },
        new Dictionary<string, object> { ["id"] = "p6", ["name"] = "Frank" },
        new Dictionary<string, object> { ["id"] = "p7", ["name"] = "Grace" },
        new Dictionary<string, object> { ["id"] = "p8", ["name"] = "Heidi" },
        new Dictionary<string, object> { ["id"] = "p9", ["name"] = "Ivan" },
        new Dictionary<string, object> { ["id"] = "p10", ["name"] = "Judy" },
        new Dictionary<string, object> { ["id"] = "p11", ["name"] = "Mallory" },
        new Dictionary<string, object> { ["id"] = "p12", ["name"] = "Niaj" },
    };

    private static readonly IReadOnlyList<object> Companies = new List<object>
    {
        new Dictionary<string, object> { ["id"] = "c1", ["name"] = "Acme Corp" },
        new Dictionary<string, object> { ["id"] = "c2", ["name"] = "Globex Inc" },
        new Dictionary<string, object> { ["id"] = "c3", ["name"] = "Initech" },
    };

    private static readonly IReadOnlyList<object> WorksAt = new List<object>
    {
        Work("p1", "c1", "Engineer", 2019),
        Work("p2", "c1", "Engineer", 2020),
        Work("p3", "c1", "Product Manager", 2021),
        Work("p4", "c1", "Designer", 2022),
        Work("p5", "c2", "Engineer", 2018),
        Work("p6", "c2", "Engineering Manager", 2017),
        Work("p7", "c2", "Engineer", 2021),
        Work("p8", "c2", "Recruiter", 2022),
        Work("p9", "c3", "Engineer", 2016),
        Work("p10", "c3", "Engineer", 2019),
        Work("p11", "c3", "Sales", 2020),
        Work("p12", "c3", "Sales", 2023),
    };

    // Three near-complete clusters (Alice/Bob/Carol/Dave, Eve/Frank/Grace/Heidi,
    // Ivan/Judy/Mallory/Niaj) joined only by two sparse bridges (Dave-Eve,
    // Heidi-Ivan), so Louvain has a clean 3-community structure to recover.
    private static readonly IReadOnlyList<object> Knows = new List<object>
    {
        // Cluster A: Alice, Bob, Carol, Dave -- fully connected
        Know("p1", "p2", 2018), Know("p1", "p3", 2018), Know("p1", "p4", 2019),
        Know("p2", "p3", 2019), Know("p2", "p4", 2020), Know("p3", "p4", 2020),

        // Cluster B: Eve, Frank, Grace, Heidi -- fully connected
        Know("p5", "p6", 2017), Know("p5", "p7", 2018), Know("p5", "p8", 2019),
        Know("p6", "p7", 2018), Know("p6", "p8", 2019), Know("p7", "p8", 2020),

        // Cluster C: Ivan, Judy, Mallory, Niaj -- fully connected
        Know("p9", "p10", 2016), Know("p9", "p11", 2017), Know("p9", "p12", 2018),
        Know("p10", "p11", 2017), Know("p10", "p12", 2019), Know("p11", "p12", 2020),

        // Sparse bridges between clusters
        Know("p4", "p5", 2021),  // Dave -- Eve  (A to B)
        Know("p8", "p9", 2021),  // Heidi -- Ivan (B to C)
    };

    // Alice is deliberately over-followed (8 incoming FOLLOWS, including
    // from Frank who is himself followed) so PageRank has an unambiguous
    // top result. Bob and Carol follow an identical set {Alice, Frank,
    // Grace}, giving Node Similarity a clear highest-scoring pair; Mallory
    // follows only Ivan, sharing nothing with Bob, for a clear low score.
    private static readonly IReadOnlyList<object> Follows = new List<object>
    {
        Follow("p2", "p1"),  // Bob -> Alice
        Follow("p3", "p1"),  // Carol -> Alice
        Follow("p4", "p1"),  // Dave -> Alice
        Follow("p5", "p1"),  // Eve -> Alice
        Follow("p6", "p1"),  // Frank -> Alice
        Follow("p7", "p1"),  // Grace -> Alice
        Follow("p9", "p1"),  // Ivan -> Alice
        Follow("p10", "p1"), // Judy -> Alice

        Follow("p2", "p6"),  // Bob -> Frank
        Follow("p3", "p6"),  // Carol -> Frank
        Follow("p2", "p7"),  // Bob -> Grace
        Follow("p3", "p7"),  // Carol -> Grace
        Follow("p8", "p6"),  // Heidi -> Frank
        Follow("p1", "p6"),  // Alice -> Frank

        Follow("p11", "p9"), // Mallory -> Ivan
        Follow("p12", "p9"), // Niaj -> Ivan
    };

    private static Dictionary<string, object> Work(string personId, string companyId, string role, long since) =>
        new()
        {
            ["personId"] = personId,
            ["companyId"] = companyId,
            ["role"] = role,
            ["since"] = since,
        };

    private static Dictionary<string, object> Know(string fromId, string toId, long since) =>
        new()
        {
            ["fromId"] = fromId,
            ["toId"] = toId,
            ["since"] = since,
        };

    private static Dictionary<string, object> Follow(string fromId, string toId) =>
        new()
        {
            ["fromId"] = fromId,
            ["toId"] = toId,
        };
}
