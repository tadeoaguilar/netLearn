using GraphAlgorithms.Gds;
using GraphAlgorithms.Models;
using Neo4j.Driver;

namespace GraphAlgorithms.Algorithms;

/// <summary>
/// Runs Louvain community detection against a projected graph and maps the
/// results to <see cref="CommunityMembership"/>. Louvain groups nodes into
/// communities by maximizing modularity -- densely-connected clusters end
/// up in the same community, with only a few "bridge" edges crossing
/// between communities.
/// </summary>
public static class LouvainRunner
{
    public static async Task<IReadOnlyList<CommunityMembership>> RunAsync(IAsyncSession session, string graphName)
    {
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(CypherQueries.StreamLouvain(), new { graphName });
            var records = await cursor.ToListAsync();
            return (IReadOnlyList<CommunityMembership>)records
                .Select(r => new CommunityMembership(
                    Neo4j.Driver.ValueExtensions.As<string>(r["name"]),
                    Neo4j.Driver.ValueExtensions.As<long>(r["communityId"])))
                .ToList();
        });
    }
}
