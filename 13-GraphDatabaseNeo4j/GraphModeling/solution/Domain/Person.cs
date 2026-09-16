namespace GraphModeling.Domain;

/// <summary>
/// A person in the shared professional-network domain used by every
/// project in this module. Id is a stable business key (not a
/// database-generated identity) -- it's what the Person.id uniqueness
/// constraint in <see cref="Persistence.GraphSchema"/> protects.
/// </summary>
public sealed record Person(string Id, string Name);
