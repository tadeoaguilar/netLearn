using GraphModeling.Domain;
using Neo4j.Driver;

namespace GraphModeling.Persistence;

/// <summary>
/// The shared professional-network dataset used by every project in this
/// module: GraphQuerying, GraphTraversals, GraphAlgorithms, and
/// GraphTransactions all assume this shape exists (though each of those
/// projects seeds its own, separate Neo4j container -- nothing here is
/// shared at the database level, only the modeling decisions are).
///
/// Every write goes through <see cref="GraphWriter"/>, which uses `MERGE`
/// for both nodes and relationships. That makes <see cref="SeedAsync"/>
/// idempotent: running it once or a hundred times against the same
/// database produces the exact same graph, which is what lets a learner
/// re-run it fearlessly while iterating instead of needing to reset the
/// container each time.
///
/// The mesh is deliberately uneven: p1 (Alice) and p9 (Ingrid) are
/// well-connected "hub" people -- lots of KNOWS edges, lots of incoming
/// FOLLOWS -- while p10/p11 form a small, mostly-isolated cluster off to
/// the side. That unevenness is what makes later projects' traversal and
/// algorithm work (shortest paths, PageRank, community detection) show
/// something other than "everything is equally connected to everything."
/// </summary>
public static class GraphSeeder
{
    private static readonly Person[] People =
    [
        new("p1", "Alice Anderson"),
        new("p2", "Bob Brennan"),
        new("p3", "Carla Chen"),
        new("p4", "David Diaz"),
        new("p5", "Elena Evans"),
        new("p6", "Frank Foster"),
        new("p7", "Grace Gomez"),
        new("p8", "Hassan Hussain"),
        new("p9", "Ingrid Iversen"),
        new("p10", "Jamal Jackson"),
        new("p11", "Katrin Kessler"),
        new("p12", "Liam Lopez"),
        new("p13", "Maya Martin"),
        new("p14", "Noah Nguyen"),
    ];

    private static readonly Company[] Companies =
    [
        new("c1", "Acme Robotics"),
        new("c2", "Globex Analytics"),
        new("c3", "Initech Software"),
        new("c4", "Umbrella Health"),
    ];

    private static readonly (string PersonId, string CompanyId, string Role, int Since)[] WorksAt =
    [
        ("p1", "c1", "Engineering Lead", 2018),
        ("p2", "c1", "Senior Engineer", 2019),
        ("p3", "c1", "Product Manager", 2020),
        ("p12", "c1", "Intern", 2023),
        ("p4", "c2", "Data Scientist", 2017),
        ("p5", "c2", "ML Engineer", 2021),
        ("p6", "c2", "Analytics Director", 2016),
        ("p13", "c2", "Sales Representative", 2019),
        ("p7", "c3", "Software Engineer", 2019),
        ("p8", "c3", "QA Engineer", 2020),
        ("p9", "c3", "CTO", 2015),
        ("p14", "c3", "DevOps Engineer", 2021),
        ("p10", "c4", "Research Scientist", 2018),
        ("p11", "c4", "Lab Technician", 2022),
    ];

    // Conceptually symmetric -- listed in whatever order reads naturally;
    // GraphWriter.CreateKnowsAsync normalizes the actual edge direction.
    private static readonly (string PersonAId, string PersonBId, int Since)[] Knows =
    [
        ("p1", "p2", 2018),
        ("p1", "p3", 2019),
        ("p1", "p4", 2020),
        ("p1", "p6", 2021),
        ("p1", "p9", 2015),
        ("p1", "p12", 2023),
        ("p9", "p7", 2019),
        ("p9", "p8", 2020),
        ("p9", "p14", 2021),
        ("p9", "p6", 2016),
        ("p2", "p3", 2019),
        ("p3", "p12", 2023),
        ("p4", "p5", 2021),
        ("p7", "p8", 2019),
        ("p10", "p11", 2022),
        ("p13", "p6", 2019),
    ];

    // Directed and asymmetric by nature. p1 and p9 are followed by many
    // and follow few -- an "influencer" shape that later projects'
    // PageRank exercise depends on.
    private static readonly (string FollowerId, string FolloweeId)[] Follows =
    [
        ("p2", "p1"),
        ("p3", "p1"),
        ("p4", "p1"),
        ("p5", "p1"),
        ("p7", "p1"),
        ("p10", "p1"),
        ("p13", "p1"),
        ("p1", "p9"),
        ("p1", "p6"),
        ("p6", "p9"),
        ("p8", "p9"),
        ("p14", "p9"),
        ("p12", "p3"),
        ("p11", "p10"),
        ("p5", "p4"),
        ("p3", "p2"),
    ];

    public static async Task SeedAsync(IAsyncSession session)
    {
        foreach (var person in People)
        {
            await GraphWriter.CreatePersonAsync(session, person);
        }

        foreach (var company in Companies)
        {
            await GraphWriter.CreateCompanyAsync(session, company);
        }

        foreach (var (personId, companyId, role, since) in WorksAt)
        {
            await GraphWriter.CreateWorksAtAsync(session, personId, companyId, role, since);
        }

        foreach (var (personAId, personBId, since) in Knows)
        {
            await GraphWriter.CreateKnowsAsync(session, personAId, personBId, since);
        }

        foreach (var (followerId, followeeId) in Follows)
        {
            await GraphWriter.CreateFollowsAsync(session, followerId, followeeId);
        }
    }
}
