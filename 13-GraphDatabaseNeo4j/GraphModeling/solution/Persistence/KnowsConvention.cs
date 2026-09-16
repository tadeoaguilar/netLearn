namespace GraphModeling.Persistence;

/// <summary>
/// KNOWS is conceptually symmetric -- if Alice knows Bob, Bob knows Alice
/// too, by definition -- but every relationship in Cypher is directed.
/// There are two honest ways to model that:
///
///   1. Two directed edges, one each way. Queries can then match either
///      direction naturally, but every write has to create (or delete)
///      both edges together, and nothing in the database stops them from
///      drifting apart if some code path forgets the second write.
///   2. One directed edge, with a documented convention for which
///      direction it's created in, and every query matching it with an
///      undirected pattern -- `(a)-[:KNOWS]-(b)`, no arrowhead -- instead
///      of a directed one.
///
/// This module picks option 2: always create the edge FROM the person
/// with the lexicographically smaller Id. That halves the storage and
/// removes the "did both writes happen" failure mode entirely, at the
/// cost of every KNOWS query needing to remember to match it undirected
/// (a directed `(a)-[:KNOWS]->(b)` query would silently miss half of a
/// person's KNOWS edges -- whichever half happened to have the smaller
/// id on the other side).
/// </summary>
public static class KnowsConvention
{
    /// <summary>
    /// Given two person ids in any order, returns which one KNOWS should
    /// be created FROM and which one it should be created TO.
    /// </summary>
    public static (string FromId, string ToId) Resolve(string personAId, string personBId)
    {
        if (personAId == personBId)
        {
            throw new ArgumentException("A person cannot KNOW themselves.");
        }

        return string.CompareOrdinal(personAId, personBId) < 0
            ? (personAId, personBId)
            : (personBId, personAId);
    }
}
