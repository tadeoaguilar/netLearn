namespace GraphAlgorithms.Gds;

/// <summary>
/// Names used for the projected (in-memory) graphs in GDS's graph catalog.
/// A projected graph is independent of the live graph in the database --
/// it lives under one of these names until it's explicitly dropped (see
/// EXERCISE.md Part 2 for why that matters).
/// </summary>
public static class GraphNames
{
    /// <summary>Person nodes + FOLLOWS relationships, natural (directed) orientation.</summary>
    public const string SocialNetwork = "social-network";

    /// <summary>Person nodes + KNOWS relationships, undirected orientation.</summary>
    public const string FriendGroups = "friend-groups";
}
