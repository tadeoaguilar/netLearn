using GraphQuerying.Seed;
using Neo4j.Driver;

namespace GraphQuerying.Tests;

[Collection("Neo4j")]
public class PatternMatchingTests(Neo4jFixture fixture)
{
    [Fact]
    public async Task Inline_pattern_property_and_WHERE_clause_return_the_same_node()
    {
        await using var session = fixture.Driver.AsyncSession();
        var expected = GraphSeeder.People[5];

        var inlineName = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person {id: $id}) RETURN p.name AS name",
                new { id = expected.Id });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<string>(record["name"]);
        });

        var whereName = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person) WHERE p.id = $id RETURN p.name AS name",
                new { id = expected.Id });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<string>(record["name"]);
        });

        inlineName.Should().Be(expected.Name);
        whereName.Should().Be(expected.Name);
    }

    [Fact]
    public async Task Filtering_by_relationship_property_matches_generated_seed_data()
    {
        await using var session = fixture.Driver.AsyncSession();
        const int sinceYear = 2020;
        var company = GraphSeeder.Companies[0];

        var expected = GraphSeeder.WorksAtEdges.Count(e => e.CompanyId == company.Id && e.Since >= sinceYear);

        var actual = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person)-[r:WORKS_AT]->(c:Company {id: $companyId})
                WHERE r.since >= $sinceYear
                RETURN count(p) AS total
                """,
                new { companyId = company.Id, sinceYear });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<long>(record["total"]);
        });

        actual.Should().Be(expected);
    }

    [Fact]
    public async Task KNOWS_relationship_property_filter_matches_generated_seed_data()
    {
        await using var session = fixture.Driver.AsyncSession();
        const int year = 2020;

        var expected = GraphSeeder.KnowsEdges.Count(e => e.Since > year);

        var actual = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (:Person)-[r:KNOWS]-(:Person) WHERE r.since > $year RETURN count(r) AS total",
                new { year });
            var record = await cursor.SingleAsync();
            // Every undirected edge is traversed from both ends.
            return Neo4j.Driver.ValueExtensions.As<long>(record["total"]) / 2;
        });

        actual.Should().Be(expected);
    }

    [Fact]
    public async Task Multiple_relationship_types_in_one_pattern_matches_the_union_of_KNOWS_and_FOLLOWS()
    {
        await using var session = fixture.Driver.AsyncSession();
        var personId = GraphSeeder.People[0].Id;

        var expectedOthers = new HashSet<string>();
        foreach (var edge in GraphSeeder.KnowsEdges)
        {
            if (edge.PersonAId == personId)
            {
                expectedOthers.Add(edge.PersonBId);
            }
            else if (edge.PersonBId == personId)
            {
                expectedOthers.Add(edge.PersonAId);
            }
        }

        foreach (var edge in GraphSeeder.FollowsEdges)
        {
            if (edge.FollowerId == personId)
            {
                expectedOthers.Add(edge.FolloweeId);
            }
            else if (edge.FolloweeId == personId)
            {
                expectedOthers.Add(edge.FollowerId);
            }
        }

        var actual = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (:Person {id: $id})-[:KNOWS|FOLLOWS]-(other:Person) RETURN count(DISTINCT other) AS total",
                new { id = personId });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<long>(record["total"]);
        });

        actual.Should().Be(expectedOthers.Count);
    }

    [Fact]
    public async Task Node_property_filter_excludes_non_matching_nodes()
    {
        await using var session = fixture.Driver.AsyncSession();
        var target = GraphSeeder.People[10];

        var matchedIds = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person) WHERE p.name = $name RETURN p.id AS id",
                new { name = target.Name });
            var records = await cursor.ToListAsync();
            return records.Select(r => Neo4j.Driver.ValueExtensions.As<string>(r["id"])).ToList();
        });

        matchedIds.Should().Contain(target.Id);
        matchedIds.Should().OnlyContain(id => id == target.Id || GraphSeeder.People.First(p => p.Id == id).Name == target.Name);
    }
}
