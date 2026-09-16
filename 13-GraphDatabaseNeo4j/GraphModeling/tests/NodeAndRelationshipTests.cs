using GraphModeling.Domain;
using GraphModeling.Persistence;
using Neo4j.Driver;
using static GraphModeling.Tests.CypherAssertionHelpers;

namespace GraphModeling.Tests;

/// <summary>
/// Parts 1-3 of EXERCISE.md: nodes and labels, relationships as
/// first-class data (WORKS_AT with properties), and directionality
/// (FOLLOWS vs. KNOWS). Every test here creates its own uniquely-id'd
/// nodes -- these run against the same shared, already-seeded container
/// as every other test class in this project, so reusing a seeded id
/// like "p1" here would collide with <see cref="SeedDataTests"/>'s
/// assumptions about the seed data's shape.
/// </summary>
[Collection("Neo4j graph")]
public class NodeAndRelationshipTests(Neo4jSharedFixture fixture)
{
    [Fact]
    public async Task Can_create_and_read_back_a_person_node()
    {
        await using var session = fixture.Driver.AsyncSession();
        var person = new Person($"test-person-{Guid.NewGuid()}", "Zoe Zimmerman");

        await GraphWriter.CreatePersonAsync(session, person);

        var name = await ReadPersonNameAsync(session, person.Id);
        name.Should().Be("Zoe Zimmerman");
    }

    [Fact]
    public async Task Can_create_and_read_back_a_company_node()
    {
        await using var session = fixture.Driver.AsyncSession();
        var company = new Company($"test-company-{Guid.NewGuid()}", "Soylent Corp");

        await GraphWriter.CreateCompanyAsync(session, company);

        var name = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (c:Company {id: $id}) RETURN c.name AS name", new { id = company.Id });
            var record = await cursor.SingleAsync();
            return ValueExtensions.As<string>(record["name"]);
        });

        name.Should().Be("Soylent Corp");
    }

    [Fact]
    public async Task Parameterized_query_stores_malicious_looking_input_as_plain_data()
    {
        await using var session = fixture.Driver.AsyncSession();
        const string maliciousName = "Robert'); MATCH (n) DETACH DELETE n //";
        var person = new Person($"test-injection-{Guid.NewGuid()}", maliciousName);

        await GraphWriter.CreatePersonAsync(session, person);

        // If GraphWriter built its Cypher by string concatenation instead
        // of the $name parameter, this value would have executed as a
        // second statement and wiped out every node in the shared
        // container -- including the whole seeded dataset every other
        // test class here depends on.
        var name = await ReadPersonNameAsync(session, person.Id);
        name.Should().Be(maliciousName);

        var totalNodes = await CountAsync(session, "MATCH (n) RETURN count(n) AS c");
        totalNodes.Should().BeGreaterThan(1, "the seeded dataset and other tests' nodes must still exist");
    }

    [Fact]
    public async Task Works_at_relationship_properties_round_trip()
    {
        await using var session = fixture.Driver.AsyncSession();
        var person = new Person($"test-emp-{Guid.NewGuid()}", "Test Employee");
        var company = new Company($"test-co-{Guid.NewGuid()}", "Test Co");

        await GraphWriter.CreatePersonAsync(session, person);
        await GraphWriter.CreateCompanyAsync(session, company);
        await GraphWriter.CreateWorksAtAsync(session, person.Id, company.Id, "Engineer", 2022);

        var (role, sinceYear) = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person {id: $personId})-[r:WORKS_AT]->(c:Company {id: $companyId})
                RETURN r.role AS role, r.since AS since
                """,
                new { personId = person.Id, companyId = company.Id });
            var record = await cursor.SingleAsync();
            return (ValueExtensions.As<string>(record["role"]), ValueExtensions.As<long>(record["since"]));
        });

        role.Should().Be("Engineer");
        sinceYear.Should().Be(2022);
    }

    [Fact]
    public async Task Follows_relationship_is_directed_and_asymmetric()
    {
        await using var session = fixture.Driver.AsyncSession();
        var follower = new Person($"test-follower-{Guid.NewGuid()}", "Follower");
        var followee = new Person($"test-followee-{Guid.NewGuid()}", "Followee");

        await GraphWriter.CreatePersonAsync(session, follower);
        await GraphWriter.CreatePersonAsync(session, followee);
        await GraphWriter.CreateFollowsAsync(session, follower.Id, followee.Id);

        var forwardCount = await CountAsync(
            session,
            "MATCH (:Person {id: $a})-[:FOLLOWS]->(:Person {id: $b}) RETURN count(*) AS c",
            new { a = follower.Id, b = followee.Id });
        var reverseCount = await CountAsync(
            session,
            "MATCH (:Person {id: $b})-[:FOLLOWS]->(:Person {id: $a}) RETURN count(*) AS c",
            new { a = follower.Id, b = followee.Id });

        forwardCount.Should().Be(1);
        reverseCount.Should().Be(0, "FOLLOWS is directed -- creating A->B must not imply B->A");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Knows_relationship_direction_is_normalized_regardless_of_call_order(bool swapArguments)
    {
        await using var session = fixture.Driver.AsyncSession();
        var suffix = Guid.NewGuid().ToString("N");
        // "test-knows-a-" sorts before "test-knows-b-" for any shared
        // suffix, so the expected direction is deterministic.
        var smallerId = $"test-knows-a-{suffix}";
        var largerId = $"test-knows-b-{suffix}";

        await GraphWriter.CreatePersonAsync(session, new Person(smallerId, "A"));
        await GraphWriter.CreatePersonAsync(session, new Person(largerId, "B"));

        if (swapArguments)
        {
            await GraphWriter.CreateKnowsAsync(session, largerId, smallerId, 2020);
        }
        else
        {
            await GraphWriter.CreateKnowsAsync(session, smallerId, largerId, 2020);
        }

        var forwardCount = await CountAsync(
            session,
            "MATCH (:Person {id: $smaller})-[:KNOWS]->(:Person {id: $larger}) RETURN count(*) AS c",
            new { smaller = smallerId, larger = largerId });
        var reverseCount = await CountAsync(
            session,
            "MATCH (:Person {id: $larger})-[:KNOWS]->(:Person {id: $smaller}) RETURN count(*) AS c",
            new { smaller = smallerId, larger = largerId });

        forwardCount.Should().Be(1, "the lexicographically smaller id must always be the KNOWS source");
        reverseCount.Should().Be(0);
    }

    private static async Task<string> ReadPersonNameAsync(IAsyncSession session, string id)
    {
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:Person {id: $id}) RETURN p.name AS name", new { id });
            var record = await cursor.SingleAsync();
            return ValueExtensions.As<string>(record["name"]);
        });
    }
}
