using Neo4j.Driver;

namespace GraphModeling.Persistence;

/// <summary>
/// Cypher's DDL equivalent: uniqueness constraints and property indexes.
///
/// The uniqueness constraint on Person.id (and Company.id) is the graph
/// analogue of a relational primary key -- without it, two concurrent
/// `CREATE (p:Person {id: 'p1', ...})` calls would happily create two
/// distinct nodes that both claim to be "p1", and every relationship
/// pointing at "p1" would silently only ever reach one of them. `MERGE`
/// alone doesn't prevent this on its own under concurrent writes; the
/// constraint is what makes it a database-enforced guarantee rather than
/// an application convention.
///
/// The index on Person.name is a different kind of thing entirely: it
/// enforces nothing. It exists purely so `MATCH (p:Person {name: $name})`
/// doesn't have to scan every Person node in the graph.
/// </summary>
public static class GraphSchema
{
    public const string PersonIdConstraintName = "person_id_unique";
    public const string CompanyIdConstraintName = "company_id_unique";
    public const string PersonNameIndexName = "person_name_index";

    public static async Task EnsureConstraintsAndIndexesAsync(IAsyncSession session)
    {
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $"CREATE CONSTRAINT {PersonIdConstraintName} IF NOT EXISTS " +
                "FOR (p:Person) REQUIRE p.id IS UNIQUE");
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $"CREATE CONSTRAINT {CompanyIdConstraintName} IF NOT EXISTS " +
                "FOR (c:Company) REQUIRE c.id IS UNIQUE");
            await cursor.ConsumeAsync();
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $"CREATE INDEX {PersonNameIndexName} IF NOT EXISTS " +
                "FOR (p:Person) ON (p.name)");
            await cursor.ConsumeAsync();
        });
    }
}
