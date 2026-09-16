using GraphQuerying.Domain;
using Neo4j.Driver;

namespace GraphQuerying.Seed;

public record WorksAtEdge(string PersonId, string CompanyId, string Role, int Since);

public record KnowsEdge(string PersonAId, string PersonBId, int Since);

public record FollowsEdge(string FollowerId, string FolloweeId);

/// <summary>
/// Deterministic, idempotent seed data for a small professional network:
/// 50 Person nodes, 6 Company nodes, and a dense mesh of KNOWS/WORKS_AT/
/// FOLLOWS relationships.
///
/// Every list below is generated once from a fixed random seed, so:
/// - Re-running <see cref="SeedAsync"/> against the same database is safe --
///   every write is a MERGE, keyed on a stable id.
/// - Tests (and this project's demos) can assert against these exact,
///   known values instead of re-deriving "expected" results by querying the
///   database and hoping it agrees with itself.
/// </summary>
public static class GraphSeeder
{
    public const int PersonCount = 50;

    private static readonly string[] FirstNames =
    [
        "Ava", "Liam", "Sophia", "Noah", "Isabella", "Mason", "Mia", "Ethan", "Amelia", "Lucas",
        "Harper", "Oliver", "Evelyn", "Elijah", "Charlotte", "James", "Abigail", "Benjamin", "Emily", "Logan",
        "Ella", "Alexander", "Scarlett", "Michael", "Grace", "Daniel", "Chloe", "Henry", "Victoria", "Jackson",
        "Aria", "Sebastian", "Zoey", "Jack", "Penelope", "Owen", "Lily", "Wyatt", "Layla", "Luke",
        "Nora", "Gabriel", "Hazel", "Anthony", "Violet", "Dylan", "Aurora", "Leo", "Savannah", "Julian",
    ];

    private static readonly string[] LastNames =
    [
        "Nguyen", "Smith", "Garcia", "Kim", "Patel", "Johnson", "Chen", "Rossi", "Muller", "Silva",
        "Kowalski", "Andersen", "Dubois", "Ivanov", "Yamamoto", "Costa", "Fischer", "Novak", "Haddad", "Suzuki",
    ];

    private static readonly string[] Roles =
    [
        "Software Engineer", "Senior Software Engineer", "Engineering Manager", "Product Manager",
        "Data Scientist", "UX Designer", "Sales Representative", "Recruiter", "VP of Engineering", "CTO",
    ];

    // Declaration order matters: these are static properties with
    // initializers, which C# runs top-to-bottom, so People and Companies
    // are guaranteed to exist before the edge lists that reference them.
    public static IReadOnlyList<Person> People { get; } = GeneratePeople();

    public static IReadOnlyList<Company> Companies { get; } =
    [
        new("c0", "Initech"),
        new("c1", "Globex"),
        new("c2", "Umbrella Corp"),
        new("c3", "Wayne Enterprises"),
        new("c4", "Stark Industries"),
        new("c5", "Wonka Industries"),
    ];

    public static IReadOnlyList<WorksAtEdge> WorksAtEdges { get; } = GenerateWorksAt();

    public static IReadOnlyList<KnowsEdge> KnowsEdges { get; } = GenerateKnows();

    public static IReadOnlyList<FollowsEdge> FollowsEdges { get; } = GenerateFollows();

    public static async Task SeedAsync(IDriver driver)
    {
        await using var session = driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            await (await tx.RunAsync(
                    "CREATE CONSTRAINT person_id_unique IF NOT EXISTS FOR (p:Person) REQUIRE p.id IS UNIQUE"))
                .ConsumeAsync();
            await (await tx.RunAsync(
                    "CREATE CONSTRAINT company_id_unique IF NOT EXISTS FOR (c:Company) REQUIRE c.id IS UNIQUE"))
                .ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            await (await tx.RunAsync(
                    "UNWIND $rows AS row MERGE (p:Person {id: row.id}) SET p.name = row.name",
                    new
                    {
                        rows = People
                            .Select(p => new Dictionary<string, object> { ["id"] = p.Id, ["name"] = p.Name })
                            .ToList(),
                    }))
                .ConsumeAsync();

            await (await tx.RunAsync(
                    "UNWIND $rows AS row MERGE (c:Company {id: row.id}) SET c.name = row.name",
                    new
                    {
                        rows = Companies
                            .Select(c => new Dictionary<string, object> { ["id"] = c.Id, ["name"] = c.Name })
                            .ToList(),
                    }))
                .ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            await (await tx.RunAsync(
                    """
                    UNWIND $rows AS row
                    MATCH (p:Person {id: row.personId}), (c:Company {id: row.companyId})
                    MERGE (p)-[r:WORKS_AT]->(c)
                    SET r.role = row.role, r.since = row.since
                    """,
                    new
                    {
                        rows = WorksAtEdges.Select(w => new Dictionary<string, object>
                        {
                            ["personId"] = w.PersonId,
                            ["companyId"] = w.CompanyId,
                            ["role"] = w.Role,
                            ["since"] = w.Since,
                        }).ToList(),
                    }))
                .ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            // Undirected MERGE: KNOWS models a symmetric relationship. Neo4j
            // still stores it with *some* internal direction, but matching
            // it with "-[:KNOWS]-" (no arrow) treats it as undirected, and
            // MERGE with no arrow finds an existing edge in either direction
            // before creating a new one.
            await (await tx.RunAsync(
                    """
                    UNWIND $rows AS row
                    MATCH (a:Person {id: row.aId}), (b:Person {id: row.bId})
                    MERGE (a)-[r:KNOWS]-(b)
                    SET r.since = row.since
                    """,
                    new
                    {
                        rows = KnowsEdges.Select(k => new Dictionary<string, object>
                        {
                            ["aId"] = k.PersonAId,
                            ["bId"] = k.PersonBId,
                            ["since"] = k.Since,
                        }).ToList(),
                    }))
                .ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            await (await tx.RunAsync(
                    """
                    UNWIND $rows AS row
                    MATCH (a:Person {id: row.followerId}), (b:Person {id: row.followeeId})
                    MERGE (a)-[:FOLLOWS]->(b)
                    """,
                    new
                    {
                        rows = FollowsEdges.Select(f => new Dictionary<string, object>
                        {
                            ["followerId"] = f.FollowerId,
                            ["followeeId"] = f.FolloweeId,
                        }).ToList(),
                    }))
                .ConsumeAsync();
        });
    }

    private static List<Person> GeneratePeople()
    {
        var random = new Random(42);
        var people = new List<Person>(PersonCount);
        for (var i = 0; i < PersonCount; i++)
        {
            var first = FirstNames[random.Next(FirstNames.Length)];
            var last = LastNames[random.Next(LastNames.Length)];
            people.Add(new Person($"p{i}", $"{first} {last}"));
        }

        return people;
    }

    private static List<WorksAtEdge> GenerateWorksAt()
    {
        var random = new Random(43);

        // Uneven company sizes on purpose, so Part 5's "employees per
        // company" grouping returns visibly different counts instead of a
        // suspiciously even split.
        int[] weights = [12, 9, 6, 11, 7, 5];
        var companyIds = Companies.Select(c => c.Id).ToArray();

        var pool = new List<string>();
        for (var i = 0; i < companyIds.Length; i++)
        {
            pool.AddRange(Enumerable.Repeat(companyIds[i], weights[i]));
        }

        var assignments = pool.OrderBy(_ => random.Next()).Take(PersonCount).ToList();
        while (assignments.Count < PersonCount)
        {
            assignments.Add(companyIds[random.Next(companyIds.Length)]);
        }

        var edges = new List<WorksAtEdge>(PersonCount);
        for (var i = 0; i < PersonCount; i++)
        {
            var role = Roles[random.Next(Roles.Length)];
            var since = random.Next(2014, 2025);
            edges.Add(new WorksAtEdge(People[i].Id, assignments[i], role, since));
        }

        return edges;
    }

    private static List<KnowsEdge> GenerateKnows()
    {
        var random = new Random(44);
        var edges = new List<KnowsEdge>();

        // Every unordered pair of people has a 12% chance of knowing each
        // other -- dense enough to give aggregation/pagination queries
        // meaningfully different result sets, sparse enough to still look
        // like a real social mesh rather than a complete graph.
        for (var i = 0; i < PersonCount; i++)
        {
            for (var j = i + 1; j < PersonCount; j++)
            {
                if (random.NextDouble() < 0.12)
                {
                    edges.Add(new KnowsEdge(People[i].Id, People[j].Id, random.Next(2008, 2025)));
                }
            }
        }

        return edges;
    }

    private static List<FollowsEdge> GenerateFollows()
    {
        var random = new Random(45);
        var edges = new List<FollowsEdge>();

        // FOLLOWS is directed and asymmetric (like a social network, not a
        // mutual connection): each person follows 5-9 others, chosen
        // independently, so being followed back is not guaranteed.
        for (var i = 0; i < PersonCount; i++)
        {
            var followCount = random.Next(5, 10);
            var candidates = Enumerable.Range(0, PersonCount)
                .Where(j => j != i)
                .OrderBy(_ => random.Next())
                .Take(followCount);

            foreach (var j in candidates)
            {
                edges.Add(new FollowsEdge(People[i].Id, People[j].Id));
            }
        }

        return edges;
    }
}
