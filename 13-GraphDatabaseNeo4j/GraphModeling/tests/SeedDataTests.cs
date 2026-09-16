using GraphModeling.Persistence;
using static GraphModeling.Tests.CypherAssertionHelpers;

namespace GraphModeling.Tests;

/// <summary>
/// Part 5 of EXERCISE.md: the shared seed dataset. These tests assert
/// against the seed applied once by <see cref="Neo4jSharedFixture"/> --
/// exact counts, the KNOWS direction convention holding across the whole
/// seeded graph (not just one edge), the two hub people actually being
/// well-connected, and re-running the seed changing nothing.
/// </summary>
[Collection("Neo4j graph")]
public class SeedDataTests(Neo4jSharedFixture fixture)
{
    [Fact]
    public async Task Seed_creates_the_expected_number_of_people()
    {
        await using var session = fixture.Driver.AsyncSession();

        var count = await CountAsync(session, "MATCH (p:Person) RETURN count(p) AS c");

        count.Should().Be(14);
    }

    [Fact]
    public async Task Seed_creates_the_expected_number_of_companies()
    {
        await using var session = fixture.Driver.AsyncSession();

        var count = await CountAsync(session, "MATCH (c:Company) RETURN count(c) AS c");

        count.Should().Be(4);
    }

    [Fact]
    public async Task Reseeding_does_not_duplicate_person_or_company_nodes()
    {
        await using var session = fixture.Driver.AsyncSession();
        var peopleBefore = await CountAsync(session, "MATCH (p:Person) RETURN count(p) AS c");
        var companiesBefore = await CountAsync(session, "MATCH (c:Company) RETURN count(c) AS c");

        await GraphSeeder.SeedAsync(session);

        var peopleAfter = await CountAsync(session, "MATCH (p:Person) RETURN count(p) AS c");
        var companiesAfter = await CountAsync(session, "MATCH (c:Company) RETURN count(c) AS c");

        peopleAfter.Should().Be(peopleBefore);
        companiesAfter.Should().Be(companiesBefore);
    }

    [Fact]
    public async Task Reseeding_does_not_duplicate_relationships()
    {
        await using var session = fixture.Driver.AsyncSession();
        var worksAtBefore = await CountAsync(session, "MATCH ()-[r:WORKS_AT]->() RETURN count(r) AS c");
        var knowsBefore = await CountAsync(session, "MATCH ()-[r:KNOWS]->() RETURN count(r) AS c");
        var followsBefore = await CountAsync(session, "MATCH ()-[r:FOLLOWS]->() RETURN count(r) AS c");

        await GraphSeeder.SeedAsync(session);
        await GraphSeeder.SeedAsync(session);

        var worksAtAfter = await CountAsync(session, "MATCH ()-[r:WORKS_AT]->() RETURN count(r) AS c");
        var knowsAfter = await CountAsync(session, "MATCH ()-[r:KNOWS]->() RETURN count(r) AS c");
        var followsAfter = await CountAsync(session, "MATCH ()-[r:FOLLOWS]->() RETURN count(r) AS c");

        worksAtAfter.Should().Be(worksAtBefore);
        knowsAfter.Should().Be(knowsBefore);
        followsAfter.Should().Be(followsBefore);
    }

    [Fact]
    public async Task All_seeded_knows_relationships_go_from_the_lexicographically_smaller_id()
    {
        await using var session = fixture.Driver.AsyncSession();

        var violations = await CountAsync(
            session,
            "MATCH (a:Person)-[:KNOWS]->(b:Person) WHERE a.id > b.id RETURN count(*) AS c");

        violations.Should().Be(0, "every KNOWS edge must be created from the smaller id per the documented convention");
    }

    [Fact]
    public async Task Every_person_has_a_works_at_relationship()
    {
        await using var session = fixture.Driver.AsyncSession();

        var peopleWithoutAJob = await CountAsync(
            session,
            "MATCH (p:Person) WHERE NOT (p)-[:WORKS_AT]->(:Company) RETURN count(p) AS c");

        peopleWithoutAJob.Should().Be(0);
    }

    [Fact]
    public async Task Seeded_hub_person_alice_has_many_knows_relationships()
    {
        await using var session = fixture.Driver.AsyncSession();

        var degree = await CountAsync(
            session, "MATCH (:Person {id: 'p1'})-[:KNOWS]-() RETURN count(*) AS c");

        degree.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Seeded_hub_person_alice_is_followed_by_several_people()
    {
        await using var session = fixture.Driver.AsyncSession();

        var followerCount = await CountAsync(
            session, "MATCH (:Person)-[:FOLLOWS]->(:Person {id: 'p1'}) RETURN count(*) AS c");

        followerCount.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Seeded_hub_person_ingrid_has_many_knows_relationships()
    {
        await using var session = fixture.Driver.AsyncSession();

        var degree = await CountAsync(
            session, "MATCH (:Person {id: 'p9'})-[:KNOWS]-() RETURN count(*) AS c");

        degree.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task Seed_produces_a_small_cluster_not_directly_connected_to_the_main_hubs()
    {
        // p10 and p11 (both Umbrella Health) know each other but neither
        // has a direct KNOWS edge to either hub (p1 or p9) -- proving the
        // seed data has real cluster structure, not one flat clique.
        await using var session = fixture.Driver.AsyncSession();

        var clusterKnowsHub = await CountAsync(
            session,
            """
            MATCH (p:Person)-[:KNOWS]-(hub:Person)
            WHERE p.id IN ['p10', 'p11'] AND hub.id IN ['p1', 'p9']
            RETURN count(*) AS c
            """);

        clusterKnowsHub.Should().Be(0);

        var clusterKnowsEachOther = await CountAsync(
            session,
            "MATCH (:Person {id: 'p10'})-[:KNOWS]-(:Person {id: 'p11'}) RETURN count(*) AS c");

        clusterKnowsEachOther.Should().Be(1);
    }
}
