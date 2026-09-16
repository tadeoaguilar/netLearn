namespace GraphAlgorithms.Models;

/// <summary>
/// One row of a PageRank result, joined back to the Person it belongs to.
/// </summary>
public sealed record PersonInfluence(string Name, double Score);
