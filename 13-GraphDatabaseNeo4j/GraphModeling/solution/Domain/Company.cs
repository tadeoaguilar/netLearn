namespace GraphModeling.Domain;

/// <summary>
/// A company in the shared professional-network domain. Same shape as
/// <see cref="Person"/> on purpose -- two labels, both with a stable
/// business-key Id, is the whole vocabulary this module's node types need.
/// </summary>
public sealed record Company(string Id, string Name);
