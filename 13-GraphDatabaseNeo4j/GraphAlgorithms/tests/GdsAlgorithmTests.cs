using GraphAlgorithms.Algorithms;
using GraphAlgorithms.Gds;
using GraphAlgorithms.Results;
using GraphAlgorithms.Seeding;
using Neo4j.Driver;

namespace GraphAlgorithms.Tests;

/// <summary>
/// Integration tests against a real Neo4j + GDS container. These prove
/// that the seed data was shaped correctly: PageRank's top result must be
/// the deliberately over-followed Alice, Louvain must recover the three
/// deliberately-separated KNOWS clusters, and Node Similarity must rank
/// the deliberately-identical Bob/Carol follow sets above the
/// deliberately-disjoint Bob/Mallory pair.
///
/// This sandbox has no Docker daemon, so these cannot actually run here --
/// see the project README for how to run them for real. They compile and
/// are written exactly as they should run against a live container.
/// </summary>
[Trait("Category", "Integration")]
public class GdsAlgorithmTests : IClassFixture<GdsFixture>
{
    private readonly GdsFixture _fixture;

    public GdsAlgorithmTests(GdsFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GdsPlugin_IsLoaded_ReportsVersion()
    {
        await using var session = _fixture.Driver.AsyncSession();

        var version = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(CypherQueries.GdsVersion());
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<string>(record["version"]);
        });

        version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Seed_IsIdempotent_NodeAndRelationshipCountsStable()
    {
        await using var session = _fixture.Driver.AsyncSession();

        async Task<long> CountAsync(string relationshipType) =>
            await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync($"MATCH ()-[r:{relationshipType}]->() RETURN count(r) AS c");
                var record = await cursor.SingleAsync();
                return Neo4j.Driver.ValueExtensions.As<long>(record["c"]);
            });

        var peopleBefore = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:Person) RETURN count(p) AS c");
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<long>(record["c"]);
        });
        var knowsBefore = await CountAsync("KNOWS");
        var followsBefore = await CountAsync("FOLLOWS");

        // Re-seed. MERGE should mean nothing new gets created.
        await GraphSeeder.SeedAsync(session);

        var peopleAfter = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:Person) RETURN count(p) AS c");
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<long>(record["c"]);
        });
        var knowsAfter = await CountAsync("KNOWS");
        var followsAfter = await CountAsync("FOLLOWS");

        peopleBefore.Should().Be(12);
        peopleAfter.Should().Be(peopleBefore);
        knowsAfter.Should().Be(knowsBefore);
        followsAfter.Should().Be(followsBefore);
    }

    [Fact]
    public async Task PageRank_RanksSeededInfluencer_Highest()
    {
        await using var session = _fixture.Driver.AsyncSession();

        await GraphCatalog.ProjectSocialNetworkAsync(session);
        var scores = await PageRankRunner.RunAsync(session, GraphNames.SocialNetwork);
        var top = ResultFormatting.TopInfluencers(scores, count: 1);

        top.Should().ContainSingle().Which.Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Louvain_FindsAtLeastThreeCommunities()
    {
        await using var session = _fixture.Driver.AsyncSession();

        await GraphCatalog.ProjectFriendGroupsAsync(session);
        var memberships = await LouvainRunner.RunAsync(session, GraphNames.FriendGroups);
        var communities = ResultFormatting.GroupByCommunity(memberships);

        communities.Count.Should().BeGreaterThanOrEqualTo(3);

        // The two people joined only by a single bridging KNOWS edge should
        // not have been merged into the same community as their bridge partner's
        // whole cluster -- Dave (cluster A) and Ivan (cluster C) are the
        // farthest-apart pair in the graph.
        var daveCommunity = communities.Single(c => c.Members.Contains("Dave")).CommunityId;
        var ivanCommunity = communities.Single(c => c.Members.Contains("Ivan")).CommunityId;
        daveCommunity.Should().NotBe(ivanCommunity);
    }

    [Fact]
    public async Task NodeSimilarity_HighOverlapPairScoresHigherThanLowOverlapPair()
    {
        await using var session = _fixture.Driver.AsyncSession();

        await GraphCatalog.ProjectSocialNetworkAsync(session);
        var similarities = await NodeSimilarityRunner.RunAsync(session, GraphNames.SocialNetwork);

        double? ScoreFor(string a, string b) => similarities
            .Where(p => (p.PersonA == a && p.PersonB == b) || (p.PersonA == b && p.PersonB == a))
            .Select(p => (double?)p.Similarity)
            .FirstOrDefault();

        var bobCarol = ScoreFor("Bob", "Carol");
        var bobMallory = ScoreFor("Bob", "Mallory");

        // Bob and Carol follow an identical set of people; GDS may omit a
        // pair entirely from the stream if its similarity is exactly 0, so
        // an absent Bob/Mallory row also counts as "lower than Bob/Carol".
        bobCarol.Should().NotBeNull("Bob and Carol share every followed account");
        bobCarol!.Value.Should().BeGreaterThan(bobMallory ?? 0.0);
    }

    [Fact]
    public async Task GraphProjection_ExistsAfterProject_AndGoneAfterDrop()
    {
        await using var session = _fixture.Driver.AsyncSession();

        await GraphCatalog.ProjectSocialNetworkAsync(session);
        (await GraphCatalog.ExistsAsync(session, GraphNames.SocialNetwork)).Should().BeTrue();

        await GraphCatalog.DropIfExistsAsync(session, GraphNames.SocialNetwork);
        (await GraphCatalog.ExistsAsync(session, GraphNames.SocialNetwork)).Should().BeFalse();
    }
}
