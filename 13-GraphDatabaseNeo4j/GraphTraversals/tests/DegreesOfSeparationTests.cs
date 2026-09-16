namespace GraphTraversals.Tests;

[Collection(Neo4jCollection.Name)]
public class DegreesOfSeparationTests
{
    private readonly Neo4jFixture _fixture;

    public DegreesOfSeparationTests(Neo4jFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Alice_hop_one_group_is_her_direct_knows_connections()
    {
        var byHop = await _fixture.Queries.DegreesOfSeparationAsync("alice", maxHops: 5);

        byHop[1].Should().BeEquivalentTo(["bob", "carol", "dave", "erin", "frank"]);
    }

    [Fact]
    public async Task Alice_hop_two_group_is_the_rest_of_the_main_cluster()
    {
        var byHop = await _fixture.Queries.DegreesOfSeparationAsync("alice", maxHops: 5);

        byHop[2].Should().BeEquivalentTo(["grace", "heidi", "ivan", "judy"]);
    }

    [Fact]
    public async Task Alice_hop_three_group_is_exactly_the_bridge_node()
    {
        var byHop = await _fixture.Queries.DegreesOfSeparationAsync("alice", maxHops: 5);

        byHop[3].Should().Equal("mallory");
    }

    [Fact]
    public async Task Alice_hop_four_group_is_the_rest_of_the_island()
    {
        var byHop = await _fixture.Queries.DegreesOfSeparationAsync("alice", maxHops: 5);

        byHop[4].Should().BeEquivalentTo(["niaj", "olivia"]);
    }

    [Fact]
    public async Task Alice_hop_five_group_is_peggy_alone()
    {
        var byHop = await _fixture.Queries.DegreesOfSeparationAsync("alice", maxHops: 5);

        byHop[5].Should().Equal("peggy");
    }

    [Fact]
    public async Task Bounding_to_three_hops_omits_deeper_groups_entirely()
    {
        var byHop = await _fixture.Queries.DegreesOfSeparationAsync("alice", maxHops: 3);

        byHop.Keys.Should().BeEquivalentTo([1, 2, 3]);
        byHop.Values.SelectMany(ids => ids).Should().NotContain(["niaj", "olivia", "peggy"]);
    }

    [Fact]
    public async Task Every_person_appears_in_exactly_one_hop_group()
    {
        var byHop = await _fixture.Queries.DegreesOfSeparationAsync("alice", maxHops: 5);

        var allIds = byHop.Values.SelectMany(ids => ids).ToList();

        allIds.Should().OnlyHaveUniqueItems();
        allIds.Should().HaveCount(GraphSeeder.People.Count - 1); // everyone except alice
    }
}
