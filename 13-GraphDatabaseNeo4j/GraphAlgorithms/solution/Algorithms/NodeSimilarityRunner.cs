using GraphAlgorithms.Gds;
using GraphAlgorithms.Models;
using Neo4j.Driver;

namespace GraphAlgorithms.Algorithms;

/// <summary>
/// Runs Node Similarity (Jaccard coefficient over shared relationships)
/// against a projected graph and maps the results to
/// <see cref="SimilarityPair"/>. This is the "people you may know" use
/// case: two people who follow mostly the same set of others score high,
/// even if they don't follow (or know) each other directly.
/// </summary>
public static class NodeSimilarityRunner
{
    public static async Task<IReadOnlyList<SimilarityPair>> RunAsync(IAsyncSession session, string graphName)
    {
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(CypherQueries.StreamNodeSimilarity(), new { graphName });
            var records = await cursor.ToListAsync();
            return (IReadOnlyList<SimilarityPair>)records
                .Select(r => new SimilarityPair(
                    Neo4j.Driver.ValueExtensions.As<string>(r["personA"]),
                    Neo4j.Driver.ValueExtensions.As<string>(r["personB"]),
                    Neo4j.Driver.ValueExtensions.As<double>(r["similarity"])))
                .ToList();
        });
    }
}
