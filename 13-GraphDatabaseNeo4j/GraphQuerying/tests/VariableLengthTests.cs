using GraphQuerying.Seed;
using Neo4j.Driver;

namespace GraphQuerying.Tests;

[Collection("Neo4j")]
public class VariableLengthTests(Neo4jFixture fixture)
{
    private static async Task<HashSet<string>> ReachedIdsAsync(IDriver driver, string startId, int maxHops)
    {
        await using var session = driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            // The hop bound is a literal, not a parameter -- Cypher does not
            // allow parameterizing "*1..N" -- but maxHops here comes from a
            // fixed C# int, never from untrusted external input.
            var query =
                "MATCH (start:Person {id: $id})-[:FOLLOWS*1.." + maxHops + "]->(reached:Person) " +
                "WHERE reached <> start " +
                "RETURN DISTINCT reached.id AS id";
            var cursor = await tx.RunAsync(query, new { id = startId });
            var records = await cursor.ToListAsync();
            return records.Select(r => Neo4j.Driver.ValueExtensions.As<string>(r["id"])).ToHashSet();
        });
    }

    /// <summary>
    /// A plain C# breadth-first search over the same FOLLOWS edges the
    /// database was seeded with -- an independent ground truth to compare
    /// the Cypher variable-length query against, rather than trusting the
    /// query to check itself.
    /// </summary>
    private static HashSet<string> BreadthFirstReachable(string startId, int maxHops)
    {
        var adjacency = GraphSeeder.FollowsEdges
            .GroupBy(f => f.FollowerId)
            .ToDictionary(g => g.Key, g => g.Select(f => f.FolloweeId).ToList());

        var visited = new HashSet<string>();
        var frontier = new HashSet<string> { startId };

        for (var hop = 0; hop < maxHops; hop++)
        {
            var next = new HashSet<string>();
            foreach (var node in frontier)
            {
                if (adjacency.TryGetValue(node, out var followees))
                {
                    foreach (var followee in followees)
                    {
                        if (followee != startId)
                        {
                            next.Add(followee);
                        }
                    }
                }
            }

            visited.UnionWith(next);
            frontier = next;
        }

        return visited;
    }

    [Fact]
    public async Task One_hop_matches_direct_FOLLOWS_targets()
    {
        var startId = GraphSeeder.People[0].Id;
        var expected = GraphSeeder.FollowsEdges
            .Where(f => f.FollowerId == startId)
            .Select(f => f.FolloweeId)
            .ToHashSet();

        var actual = await ReachedIdsAsync(fixture.Driver, startId, maxHops: 1);

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Two_hop_reachability_matches_an_independent_breadth_first_search()
    {
        var startId = GraphSeeder.People[0].Id;
        var expected = BreadthFirstReachable(startId, maxHops: 2);

        var actual = await ReachedIdsAsync(fixture.Driver, startId, maxHops: 2);

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Three_hop_reachability_matches_an_independent_breadth_first_search()
    {
        var startId = GraphSeeder.People[3].Id;
        var expected = BreadthFirstReachable(startId, maxHops: 3);

        var actual = await ReachedIdsAsync(fixture.Driver, startId, maxHops: 3);

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Reachable_set_grows_or_stays_the_same_as_hop_count_increases()
    {
        var startId = GraphSeeder.People[7].Id;

        var oneHop = await ReachedIdsAsync(fixture.Driver, startId, maxHops: 1);
        var twoHop = await ReachedIdsAsync(fixture.Driver, startId, maxHops: 2);
        var threeHop = await ReachedIdsAsync(fixture.Driver, startId, maxHops: 3);

        oneHop.Should().BeSubsetOf(twoHop);
        twoHop.Should().BeSubsetOf(threeHop);
    }

    [Fact]
    public async Task Reachable_set_never_contains_the_start_node_itself()
    {
        var startId = GraphSeeder.People[0].Id;

        var reached = await ReachedIdsAsync(fixture.Driver, startId, maxHops: 3);

        reached.Should().NotContain(startId);
    }
}
