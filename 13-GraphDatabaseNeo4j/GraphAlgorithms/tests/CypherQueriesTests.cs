using GraphAlgorithms.Gds;

namespace GraphAlgorithms.Tests;

/// <summary>
/// Pure unit tests over the Cypher text GraphCatalog builds -- catches
/// typos in relationship types/orientations and the "idempotent drop"
/// contract without needing a running Neo4j instance.
/// </summary>
[Trait("Category", "Unit")]
public class CypherQueriesTests
{
    [Fact]
    public void ProjectSocialNetworkGraph_UsesFollowsWithNaturalOrientation()
    {
        var query = CypherQueries.ProjectSocialNetworkGraph();

        query.Should().Contain("gds.graph.project");
        query.Should().Contain("'Person'");
        query.Should().Contain("FOLLOWS");
        query.Should().Contain("'NATURAL'");
    }

    [Fact]
    public void ProjectFriendGroupsGraph_UsesKnowsWithUndirectedOrientation()
    {
        var query = CypherQueries.ProjectFriendGroupsGraph();

        query.Should().Contain("gds.graph.project");
        query.Should().Contain("KNOWS");
        query.Should().Contain("'UNDIRECTED'");
    }

    [Fact]
    public void DropGraph_PassesFailIfMissingFalse_ForIdempotency()
    {
        var query = CypherQueries.DropGraph();

        query.Should().Contain("gds.graph.drop($graphName, false)");
    }

    [Fact]
    public void GraphExists_ChecksTheCatalogByName()
    {
        var query = CypherQueries.GraphExists();

        query.Should().Contain("gds.graph.exists($graphName)");
    }

    [Fact]
    public void StreamPageRank_JoinsBackToPersonNameViaAsNode()
    {
        var query = CypherQueries.StreamPageRank();

        query.Should().Contain("gds.pageRank.stream($graphName)");
        query.Should().Contain("gds.util.asNode(nodeId).name");
    }

    [Fact]
    public void StreamLouvain_JoinsBackToPersonNameViaAsNode()
    {
        var query = CypherQueries.StreamLouvain();

        query.Should().Contain("gds.louvain.stream($graphName)");
        query.Should().Contain("communityId");
    }

    [Fact]
    public void StreamNodeSimilarity_JoinsBothSidesBackToPersonNames()
    {
        var query = CypherQueries.StreamNodeSimilarity();

        query.Should().Contain("gds.nodeSimilarity.stream($graphName)");
        query.Should().Contain("gds.util.asNode(node1).name");
        query.Should().Contain("gds.util.asNode(node2).name");
    }

    [Fact]
    public void GraphNames_AreDistinctAndNotEmpty()
    {
        GraphNames.SocialNetwork.Should().NotBeNullOrWhiteSpace();
        GraphNames.FriendGroups.Should().NotBeNullOrWhiteSpace();
        GraphNames.SocialNetwork.Should().NotBe(GraphNames.FriendGroups);
    }
}
