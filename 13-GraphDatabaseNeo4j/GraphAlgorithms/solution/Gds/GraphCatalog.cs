using Neo4j.Driver;

namespace GraphAlgorithms.Gds;

/// <summary>
/// Thin wrapper around the GDS graph catalog operations (project / drop /
/// exists). This is the "impure" half of the Gds namespace -- it needs a
/// live session, so it's exercised by the integration tests rather than
/// unit tests. The query text itself lives in <see cref="CypherQueries"/>
/// so that part CAN be unit tested on its own.
/// </summary>
public static class GraphCatalog
{
    public static async Task DropIfExistsAsync(IAsyncSession session, string graphName)
    {
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(CypherQueries.DropGraph(), new { graphName });
            await cursor.ConsumeAsync();
        });
    }

    public static async Task<bool> ExistsAsync(IAsyncSession session, string graphName)
    {
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(CypherQueries.GraphExists(), new { graphName });
            var record = await cursor.SingleAsync();
            return record["exists"].As<bool>();
        });
    }

    /// <summary>
    /// Projects the FOLLOWS-based "social-network" graph. Drops it first if
    /// it already exists in the catalog -- re-running this after seeding
    /// more data is exactly how you pick up a stale projection (see
    /// EXERCISE.md Part 2).
    /// </summary>
    public static async Task ProjectSocialNetworkAsync(IAsyncSession session)
    {
        await DropIfExistsAsync(session, GraphNames.SocialNetwork);
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                CypherQueries.ProjectSocialNetworkGraph(),
                new { graphName = GraphNames.SocialNetwork });
            await cursor.ConsumeAsync();
        });
    }

    /// <summary>Projects the KNOWS-based, undirected "friend-groups" graph.</summary>
    public static async Task ProjectFriendGroupsAsync(IAsyncSession session)
    {
        await DropIfExistsAsync(session, GraphNames.FriendGroups);
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                CypherQueries.ProjectFriendGroupsGraph(),
                new { graphName = GraphNames.FriendGroups });
            await cursor.ConsumeAsync();
        });
    }
}
