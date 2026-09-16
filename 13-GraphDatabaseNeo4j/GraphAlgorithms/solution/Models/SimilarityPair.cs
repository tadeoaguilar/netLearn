namespace GraphAlgorithms.Models;

/// <summary>
/// One row of a Node Similarity result: how similar two people's
/// connection sets are (Jaccard coefficient, 0.0 to 1.0).
/// </summary>
public sealed record SimilarityPair(string PersonA, string PersonB, double Similarity);
