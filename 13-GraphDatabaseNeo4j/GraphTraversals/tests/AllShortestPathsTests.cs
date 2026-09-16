namespace GraphTraversals.Tests;

[Collection(Neo4jCollection.Name)]
public class AllShortestPathsTests
{
    private readonly Neo4jFixture _fixture;

    public AllShortestPathsTests(Neo4jFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Carol_and_grace_have_exactly_two_equally_short_paths()
    {
        // carol-bob-grace and carol-judy-grace are both length 2, and
        // there is no length-1 (direct) connection between them.
        var paths = await _fixture.Queries.AllShortestPathsAsync("carol", "grace");

        paths.Should().HaveCount(2);
        paths.Should().OnlyContain(p => p.Count == 3);
        paths.Select(p => p[1]).Should().BeEquivalentTo(["bob", "judy"]);
    }

    [Fact]
    public async Task Directly_connected_people_have_exactly_one_shortest_path()
    {
        var paths = await _fixture.Queries.AllShortestPathsAsync("alice", "bob");

        paths.Should().ContainSingle();
        paths.Single().Should().Equal("alice", "bob");
    }

    [Fact]
    public async Task Every_returned_path_shares_the_same_minimal_length()
    {
        var paths = await _fixture.Queries.AllShortestPathsAsync("carol", "grace");

        paths.Select(p => p.Count).Distinct().Should().ContainSingle();
    }
}
