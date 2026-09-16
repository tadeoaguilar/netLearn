namespace GraphAlgorithms.Models;

/// <summary>
/// One row of a Louvain result: which community a Person was assigned to.
/// </summary>
public sealed record CommunityMembership(string Name, long CommunityId);
