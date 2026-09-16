namespace GraphTraversals.Tests;

[Collection(Neo4jCollection.Name)]
public class DepthBoundedTraversalTests
{
    private readonly Neo4jFixture _fixture;

    public DepthBoundedTraversalTests(Neo4jFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Within_one_hop_of_alice_returns_only_her_direct_knows()
    {
        var reachable = await _fixture.Queries.WithinHopsAsync("alice", maxHops: 1);

        reachable.Should().BeEquivalentTo(["bob", "carol", "dave", "erin", "frank"]);
    }

    [Fact]
    public async Task Within_two_hops_of_alice_reaches_the_whole_main_cluster()
    {
        var reachable = await _fixture.Queries.WithinHopsAsync("alice", maxHops: 2);

        reachable.Should().BeEquivalentTo(
            ["bob", "carol", "dave", "erin", "frank", "grace", "heidi", "ivan", "judy"]);
        reachable.Should().NotContain(["mallory", "niaj", "olivia", "peggy"]);
    }

    [Fact]
    public async Task Within_three_hops_of_alice_reaches_the_bridge_but_not_the_island_interior()
    {
        var reachable = await _fixture.Queries.WithinHopsAsync("alice", maxHops: 3);

        reachable.Should().Contain("mallory"); // exactly 3 hops away
        reachable.Should().NotContain(["niaj", "olivia", "peggy"]); // 4+ hops away
    }

    [Fact]
    public async Task Peggy_is_five_hops_from_alice_and_is_not_returned_by_a_bounded_1_to_3_query()
    {
        var reachable = await _fixture.Queries.WithinHopsAsync("alice", maxHops: 3);

        reachable.Should().NotContain("peggy");
    }

    [Fact]
    public async Task Within_five_hops_of_alice_reaches_everyone_including_peggy()
    {
        var reachable = await _fixture.Queries.WithinHopsAsync("alice", maxHops: 5);

        reachable.Should().Contain("peggy");
        reachable.Should().HaveCount(GraphSeeder.People.Count - 1); // everyone except alice herself
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_hop_bounds_are_rejected(int maxHops)
    {
        var act = () => _fixture.Queries.WithinHopsAsync("alice", maxHops);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
