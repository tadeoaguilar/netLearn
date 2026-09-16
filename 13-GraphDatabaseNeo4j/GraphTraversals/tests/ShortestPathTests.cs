namespace GraphTraversals.Tests;

[Collection(Neo4jCollection.Name)]
public class ShortestPathTests
{
    private readonly Neo4jFixture _fixture;

    public ShortestPathTests(Neo4jFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Directly_connected_people_have_a_path_of_length_one()
    {
        var path = await _fixture.Queries.ShortestPathAsync("alice", "bob");

        path.Should().Equal("alice", "bob");
    }

    [Fact]
    public async Task Erin_to_ivan_is_three_hops_through_alice_and_bob()
    {
        // Erin's only way out of her little Alice/Frank triangle is
        // through Alice, and Ivan only knows Bob, so
        // erin-alice-bob-ivan is the unique shortest path.
        var path = await _fixture.Queries.ShortestPathAsync("erin", "ivan");

        path.Should().Equal("erin", "alice", "bob", "ivan");
    }

    [Fact]
    public async Task Alice_to_peggy_crosses_the_bridge_into_the_island_in_five_hops()
    {
        var path = await _fixture.Queries.ShortestPathAsync("alice", "peggy");

        path.Should().HaveCount(6); // 5 edges = 6 nodes
        path.First().Should().Be("alice");
        path.Last().Should().Be("peggy");
        path.Should().Contain("mallory"); // the only bridge into the island
    }

    [Fact]
    public async Task Unknown_person_id_returns_an_empty_path()
    {
        var path = await _fixture.Queries.ShortestPathAsync("alice", "nobody-with-this-id");

        path.Should().BeEmpty();
    }
}
