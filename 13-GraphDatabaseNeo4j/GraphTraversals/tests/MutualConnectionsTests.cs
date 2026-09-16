namespace GraphTraversals.Tests;

[Collection(Neo4jCollection.Name)]
public class MutualConnectionsTests
{
    private readonly Neo4jFixture _fixture;

    public MutualConnectionsTests(Neo4jFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Alice_and_bob_have_exactly_one_mutual_connection()
    {
        var mutual = await _fixture.Queries.MutualConnectionsAsync("alice", "bob");

        mutual.Should().Equal("carol");
    }

    [Fact]
    public async Task Bob_and_judy_have_two_mutual_connections()
    {
        // bob knows carol and grace; judy also knows carol and grace.
        var mutual = await _fixture.Queries.MutualConnectionsAsync("bob", "judy");

        mutual.Should().BeEquivalentTo(["carol", "grace"]);
    }

    [Fact]
    public async Task Alice_and_judy_have_two_mutual_connections()
    {
        // alice knows carol and dave; judy also knows carol and dave.
        var mutual = await _fixture.Queries.MutualConnectionsAsync("alice", "judy");

        mutual.Should().BeEquivalentTo(["carol", "dave"]);
    }

    [Fact]
    public async Task Erin_and_grace_have_no_mutual_connections()
    {
        // erin only knows alice and frank; grace only knows bob and judy --
        // no overlap.
        var mutual = await _fixture.Queries.MutualConnectionsAsync("erin", "grace");

        mutual.Should().BeEmpty();
    }

    [Fact]
    public async Task Niaj_and_peggy_have_olivia_as_their_mutual_connection()
    {
        // A mutual-connections check entirely inside the island cluster.
        var mutual = await _fixture.Queries.MutualConnectionsAsync("niaj", "peggy");

        mutual.Should().Equal("olivia");
    }

    [Fact]
    public async Task Mutual_connections_result_never_includes_either_endpoint()
    {
        var mutual = await _fixture.Queries.MutualConnectionsAsync("bob", "judy");

        mutual.Should().NotContain(["bob", "judy"]);
    }
}
