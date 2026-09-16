namespace GraphTraversals.Tests;

/// <summary>
/// FOLLOWS, unlike KNOWS, is genuinely directed: these tests exist to make
/// that concrete, not just assert it in prose.
/// </summary>
[Collection(Neo4jCollection.Name)]
public class FollowsDirectionTests
{
    private readonly Neo4jFixture _fixture;

    public FollowsDirectionTests(Neo4jFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Alice_is_followed_by_carol_grace_and_heidi()
    {
        var followers = await _fixture.Queries.FollowersAsync("alice");

        followers.Should().BeEquivalentTo(["carol", "grace", "heidi"]);
    }

    [Fact]
    public async Task Alice_follows_bob_and_carol()
    {
        var following = await _fixture.Queries.FollowingAsync("alice");

        following.Should().BeEquivalentTo(["bob", "carol"]);
    }

    [Fact]
    public async Task Bob_does_not_follow_alice_back()
    {
        // Alice follows Bob, but that follow is not reciprocated -- this is
        // exactly the asymmetry a directed relationship allows and an
        // undirected one (like KNOWS) cannot represent.
        var bobFollowing = await _fixture.Queries.FollowingAsync("bob");

        bobFollowing.Should().NotContain("alice");
    }
}
