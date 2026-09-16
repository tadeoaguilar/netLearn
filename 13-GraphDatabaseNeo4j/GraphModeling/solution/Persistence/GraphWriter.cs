using GraphModeling.Domain;
using Neo4j.Driver;

namespace GraphModeling.Persistence;

/// <summary>
/// Every write here uses parameterized Cypher -- `$id`, `$name`, and so on
/// -- never string interpolation into the query text. That's the same
/// lesson as module 10's raw-SQL warnings and module 11's SQL API
/// warnings, applied to Cypher: a person's name is data, never part of
/// the query itself. See <c>NodeAndRelationshipTests.Parameterized_query_stores_malicious_looking_input_as_plain_data</c>
/// in the test project for a concrete demonstration of what a
/// string-concatenated version of this class would let a malicious name
/// do.
///
/// Nodes are created with `MERGE` (not `CREATE`) keyed on `id`, so calling
/// these methods again for the same id updates properties in place rather
/// than creating a duplicate node -- that's what makes
/// <see cref="GraphSeeder"/> idempotent. Relationships are `MERGE`d on
/// their pattern (the two endpoint nodes and the relationship type, with
/// no properties in the MERGE pattern itself), so re-running a seed
/// updates `role`/`since` on the existing edge instead of creating a
/// second one between the same two nodes.
/// </summary>
public static class GraphWriter
{
    public static async Task CreatePersonAsync(IAsyncSession session, Person person)
    {
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MERGE (p:Person {id: $id}) SET p.name = $name",
                new { id = person.Id, name = person.Name });
            await cursor.ConsumeAsync();
        });
    }

    public static async Task CreateCompanyAsync(IAsyncSession session, Company company)
    {
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MERGE (c:Company {id: $id}) SET c.name = $name",
                new { id = company.Id, name = company.Name });
            await cursor.ConsumeAsync();
        });
    }

    /// <summary>
    /// WORKS_AT is a first-class relationship with its own properties --
    /// unlike a relational foreign-key column (which can only ever hold
    /// the reference itself, never a role or a start date without an
    /// awkward junction table) or a document database's embedded array
    /// (which can't be traversed or queried independently of the document
    /// that embeds it). Here, `role` and `since` live directly on the
    /// edge, and the edge is queryable and traversable on its own terms.
    /// </summary>
    public static async Task CreateWorksAtAsync(
        IAsyncSession session, string personId, string companyId, string role, int since)
    {
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person {id: $personId})
                MATCH (c:Company {id: $companyId})
                MERGE (p)-[r:WORKS_AT]->(c)
                SET r.role = $role, r.since = $since
                """,
                new { personId, companyId, role, since });
            await cursor.ConsumeAsync();
        });
    }

    /// <summary>
    /// FOLLOWS is genuinely, inherently directed -- Alice following Bob
    /// implies nothing about whether Bob follows Alice. No convention is
    /// needed here the way it is for <see cref="CreateKnowsAsync"/>; the
    /// direction the caller passes in is the direction that's meaningful.
    /// </summary>
    public static async Task CreateFollowsAsync(IAsyncSession session, string followerId, string followeeId)
    {
        if (followerId == followeeId)
        {
            throw new ArgumentException("A person cannot follow themselves.");
        }

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (follower:Person {id: $followerId})
                MATCH (followee:Person {id: $followeeId})
                MERGE (follower)-[:FOLLOWS]->(followee)
                """,
                new { followerId, followeeId });
            await cursor.ConsumeAsync();
        });
    }

    /// <summary>
    /// KNOWS is conceptually symmetric, so the two ids can be passed in
    /// either order -- <see cref="KnowsConvention.Resolve"/> normalizes
    /// which one the edge is actually created FROM.
    /// </summary>
    public static async Task CreateKnowsAsync(IAsyncSession session, string personAId, string personBId, int since)
    {
        var (fromId, toId) = KnowsConvention.Resolve(personAId, personBId);

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $fromId})
                MATCH (b:Person {id: $toId})
                MERGE (a)-[r:KNOWS]->(b)
                SET r.since = $since
                """,
                new { fromId, toId, since });
            await cursor.ConsumeAsync();
        });
    }
}
