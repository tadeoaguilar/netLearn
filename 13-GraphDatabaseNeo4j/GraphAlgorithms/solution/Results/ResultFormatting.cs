using GraphAlgorithms.Models;

namespace GraphAlgorithms.Results;

/// <summary>
/// Pure, GDS-independent helpers for shaping algorithm results once they've
/// already been pulled out of Neo4j records. None of this touches the
/// driver or a session, so it's cheap to unit test without a container.
/// </summary>
public static class ResultFormatting
{
    /// <summary>
    /// The top <paramref name="count"/> people by PageRank score, highest first.
    /// Returns fewer than <paramref name="count"/> items if there isn't enough input.
    /// </summary>
    public static IReadOnlyList<PersonInfluence> TopInfluencers(
        IEnumerable<PersonInfluence> scores, int count)
    {
        ArgumentNullException.ThrowIfNull(scores);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        return scores
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Groups Louvain output by community id, with each community's members
    /// sorted alphabetically and communities ordered by id.
    /// </summary>
    public static IReadOnlyList<(long CommunityId, IReadOnlyList<string> Members)> GroupByCommunity(
        IEnumerable<CommunityMembership> memberships)
    {
        ArgumentNullException.ThrowIfNull(memberships);

        return memberships
            .GroupBy(m => m.CommunityId)
            .OrderBy(g => g.Key)
            .Select(g => (
                CommunityId: g.Key,
                Members: (IReadOnlyList<string>)g
                    .Select(m => m.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// The top <paramref name="count"/> most-similar pairs, highest similarity
    /// first, optionally excluding anything at or below <paramref name="minSimilarity"/>
    /// (useful for filtering out the "barely overlap" noise a real recommender
    /// wouldn't want to surface).
    /// </summary>
    public static IReadOnlyList<SimilarityPair> TopSimilarPairs(
        IEnumerable<SimilarityPair> pairs, int count, double minSimilarity = 0.0)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        return pairs
            .Where(p => p.Similarity > minSimilarity)
            .OrderByDescending(p => p.Similarity)
            .ThenBy(p => p.PersonA, StringComparer.Ordinal)
            .ThenBy(p => p.PersonB, StringComparer.Ordinal)
            .Take(count)
            .ToList();
    }
}
