using GraphAlgorithms.Models;
using GraphAlgorithms.Results;

namespace GraphAlgorithms.Tests;

/// <summary>
/// Pure unit tests for result shaping -- no Neo4j, no GDS, no container.
/// These exercise exactly the same code the solution's Program.cs uses to
/// turn raw GDS records into something printable.
/// </summary>
[Trait("Category", "Unit")]
public class ResultFormattingTests
{
    [Fact]
    public void TopInfluencers_ReturnsHighestScoresFirst()
    {
        var scores = new[]
        {
            new PersonInfluence("Alice", 2.5),
            new PersonInfluence("Bob", 1.1),
            new PersonInfluence("Carol", 1.8),
        };

        var top = ResultFormatting.TopInfluencers(scores, count: 3);

        top.Select(p => p.Name).Should().ContainInOrder("Alice", "Carol", "Bob");
    }

    [Fact]
    public void TopInfluencers_ReturnsFewerThanCount_WhenNotEnoughItems()
    {
        var scores = new[] { new PersonInfluence("Alice", 1.0) };

        var top = ResultFormatting.TopInfluencers(scores, count: 5);

        top.Should().ContainSingle().Which.Name.Should().Be("Alice");
    }

    [Fact]
    public void TopInfluencers_BreaksTiesByNameAlphabetically()
    {
        var scores = new[]
        {
            new PersonInfluence("Zoe", 1.0),
            new PersonInfluence("Amy", 1.0),
        };

        var top = ResultFormatting.TopInfluencers(scores, count: 2);

        top.Select(p => p.Name).Should().ContainInOrder("Amy", "Zoe");
    }

    [Fact]
    public void TopInfluencers_Throws_OnNegativeCount()
    {
        var act = () => ResultFormatting.TopInfluencers(Array.Empty<PersonInfluence>(), count: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GroupByCommunity_GroupsMembersByCommunityId()
    {
        var memberships = new[]
        {
            new CommunityMembership("Alice", 0),
            new CommunityMembership("Bob", 0),
            new CommunityMembership("Eve", 1),
        };

        var communities = ResultFormatting.GroupByCommunity(memberships);

        communities.Should().HaveCount(2);
        communities.Single(c => c.CommunityId == 0).Members.Should().BeEquivalentTo("Alice", "Bob");
        communities.Single(c => c.CommunityId == 1).Members.Should().BeEquivalentTo("Eve");
    }

    [Fact]
    public void GroupByCommunity_OrdersCommunitiesById_AndMembersAlphabetically()
    {
        var memberships = new[]
        {
            new CommunityMembership("Zoe", 2),
            new CommunityMembership("Bob", 0),
            new CommunityMembership("Amy", 0),
        };

        var communities = ResultFormatting.GroupByCommunity(memberships);

        communities.Select(c => c.CommunityId).Should().ContainInOrder(0L, 2L);
        communities.First(c => c.CommunityId == 0).Members.Should().ContainInOrder("Amy", "Bob");
    }

    [Fact]
    public void GroupByCommunity_ReturnsEmpty_WhenNoInput()
    {
        var communities = ResultFormatting.GroupByCommunity(Array.Empty<CommunityMembership>());

        communities.Should().BeEmpty();
    }

    [Fact]
    public void TopSimilarPairs_OrdersByScoreDescending()
    {
        var pairs = new[]
        {
            new SimilarityPair("Bob", "Mallory", 0.1),
            new SimilarityPair("Bob", "Carol", 0.9),
        };

        var top = ResultFormatting.TopSimilarPairs(pairs, count: 2);

        top.First().Should().Be(new SimilarityPair("Bob", "Carol", 0.9));
    }

    [Fact]
    public void TopSimilarPairs_ExcludesPairsAtOrBelowMinSimilarity()
    {
        var pairs = new[]
        {
            new SimilarityPair("Bob", "Carol", 1.0),
            new SimilarityPair("Bob", "Mallory", 0.0),
        };

        var top = ResultFormatting.TopSimilarPairs(pairs, count: 10, minSimilarity: 0.0);

        top.Should().ContainSingle().Which.PersonB.Should().Be("Carol");
    }

    [Fact]
    public void TopSimilarPairs_Throws_OnNegativeCount()
    {
        var act = () => ResultFormatting.TopSimilarPairs(Array.Empty<SimilarityPair>(), count: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
