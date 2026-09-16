namespace GraphAlgorithms.Gds;

/// <summary>
/// Builds the Cypher text for every GDS call this project makes. Pulled out
/// as pure string-building functions (no driver, no session) so the query
/// shape itself -- graph names, relationship types, orientations -- can be
/// unit tested without a running Neo4j instance.
/// </summary>
public static class CypherQueries
{
    /// <summary>
    /// Projects a named graph of Person nodes connected by FOLLOWS,
    /// keeping the natural (directed) orientation so PageRank and Node
    /// Similarity see "who follows whom" the same way the live graph does.
    /// </summary>
    public static string ProjectSocialNetworkGraph() =>
        $"CALL gds.graph.project($graphName, 'Person', {{FOLLOWS: {{orientation: 'NATURAL'}}}})";

    /// <summary>
    /// Projects a named graph of Person nodes connected by KNOWS, as
    /// UNDIRECTED -- friendship has no inherent direction, and Louvain's
    /// community detection expects a symmetric adjacency to group on.
    /// </summary>
    public static string ProjectFriendGroupsGraph() =>
        $"CALL gds.graph.project($graphName, 'Person', {{KNOWS: {{orientation: 'UNDIRECTED'}}}})";

    /// <summary>
    /// Drops a projected graph from the GDS catalog. <c>failIfMissing: false</c>
    /// makes this idempotent -- dropping a graph that was never (or no longer)
    /// projected is a no-op instead of an error.
    /// </summary>
    public static string DropGraph() =>
        "CALL gds.graph.drop($graphName, false) YIELD graphName RETURN graphName";

    /// <summary>Checks whether a named graph is currently in the GDS catalog.</summary>
    public static string GraphExists() =>
        "CALL gds.graph.exists($graphName) YIELD exists RETURN exists";

    /// <summary>
    /// Runs PageRank over a projected graph and joins the result straight
    /// back to the underlying Person nodes via <c>gds.util.asNode</c>.
    /// </summary>
    public static string StreamPageRank() =>
        """
        CALL gds.pageRank.stream($graphName)
        YIELD nodeId, score
        RETURN gds.util.asNode(nodeId).name AS name, score
        ORDER BY score DESC
        """;

    /// <summary>
    /// Runs Louvain community detection over a projected graph and joins
    /// the result back to Person names.
    /// </summary>
    public static string StreamLouvain() =>
        """
        CALL gds.louvain.stream($graphName)
        YIELD nodeId, communityId
        RETURN gds.util.asNode(nodeId).name AS name, communityId
        ORDER BY communityId, name
        """;

    /// <summary>
    /// Runs Node Similarity (Jaccard on shared relationships) over a
    /// projected graph and joins both sides of each pair back to names.
    /// </summary>
    public static string StreamNodeSimilarity() =>
        """
        CALL gds.nodeSimilarity.stream($graphName)
        YIELD node1, node2, similarity
        RETURN gds.util.asNode(node1).name AS personA,
               gds.util.asNode(node2).name AS personB,
               similarity
        ORDER BY similarity DESC
        """;

    /// <summary>The GDS library version, via the <c>gds.version()</c> function (not a procedure).</summary>
    public static string GdsVersion() => "RETURN gds.version() AS version";
}
