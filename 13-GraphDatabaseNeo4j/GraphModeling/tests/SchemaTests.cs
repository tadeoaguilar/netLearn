using GraphModeling.Persistence;
using Neo4j.Driver;
using static GraphModeling.Tests.CypherAssertionHelpers;

namespace GraphModeling.Tests;

/// <summary>
/// Part 4 of EXERCISE.md: uniqueness constraints and the property index.
/// These tests inspect Neo4j's own catalog (`SHOW CONSTRAINTS` /
/// `SHOW INDEXES`) rather than trusting that
/// <see cref="GraphSchema.EnsureConstraintsAndIndexesAsync"/> ran without
/// error, and then prove the constraints actually do something by
/// violating one.
/// </summary>
[Collection("Neo4j graph")]
public class SchemaTests(Neo4jSharedFixture fixture)
{
    [Fact]
    public async Task Person_id_uniqueness_constraint_exists()
    {
        await using var session = fixture.Driver.AsyncSession();

        var names = await StringColumnAsync(session, "SHOW CONSTRAINTS YIELD name RETURN name", "name");

        names.Should().Contain(GraphSchema.PersonIdConstraintName);
    }

    [Fact]
    public async Task Company_id_uniqueness_constraint_exists()
    {
        await using var session = fixture.Driver.AsyncSession();

        var names = await StringColumnAsync(session, "SHOW CONSTRAINTS YIELD name RETURN name", "name");

        names.Should().Contain(GraphSchema.CompanyIdConstraintName);
    }

    [Fact]
    public async Task Person_name_index_exists()
    {
        await using var session = fixture.Driver.AsyncSession();

        var names = await StringColumnAsync(session, "SHOW INDEXES YIELD name RETURN name", "name");

        names.Should().Contain(GraphSchema.PersonNameIndexName);
    }

    [Fact]
    public async Task Creating_a_second_person_with_a_duplicate_id_via_CREATE_violates_the_constraint()
    {
        await using var session = fixture.Driver.AsyncSession();
        var id = $"dup-person-{Guid.NewGuid()}";

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync("CREATE (p:Person {id: $id, name: 'First'})", new { id });
            await cursor.ConsumeAsync();
        });

        var act = async () => await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync("CREATE (p:Person {id: $id, name: 'Second'})", new { id });
            await cursor.ConsumeAsync();
        });

        // Without the constraint from Part 4, this second CREATE would
        // silently succeed and leave two distinct Person nodes both
        // claiming to be `id` -- exactly the bug the constraint exists to
        // make impossible.
        await act.Should().ThrowAsync<Neo4jException>();
    }

    [Fact]
    public async Task Creating_a_second_company_with_a_duplicate_id_via_CREATE_violates_the_constraint()
    {
        await using var session = fixture.Driver.AsyncSession();
        var id = $"dup-company-{Guid.NewGuid()}";

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync("CREATE (c:Company {id: $id, name: 'First'})", new { id });
            await cursor.ConsumeAsync();
        });

        var act = async () => await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync("CREATE (c:Company {id: $id, name: 'Second'})", new { id });
            await cursor.ConsumeAsync();
        });

        await act.Should().ThrowAsync<Neo4jException>();
    }
}
