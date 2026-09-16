using GraphAlgorithms.Gds;
using GraphAlgorithms.Models;
using Neo4j.Driver;

namespace GraphAlgorithms.Algorithms;

/// <summary>
/// Runs PageRank against a projected graph and maps the results to
/// <see cref="PersonInfluence"/>. PageRank measures influence, not raw
/// in-degree: a person followed by a handful of highly-followed people can
/// outrank a person followed by many low-influence people, because rank
/// flows recursively through the graph, not just by counting edges.
/// </summary>
public static class PageRankRunner
{
    public static async Task<IReadOnlyList<PersonInfluence>> RunAsync(IAsyncSession session, string graphName)
    {
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(CypherQueries.StreamPageRank(), new { graphName });
            var records = await cursor.ToListAsync();
            return (IReadOnlyList<PersonInfluence>)records
                .Select(r => new PersonInfluence(
                    Neo4j.Driver.ValueExtensions.As<string>(r["name"]),
                    Neo4j.Driver.ValueExtensions.As<double>(r["score"])))
                .ToList();
        });
    }
}
