using GraphQuerying.Seed;
using Neo4j.Driver;

namespace GraphQuerying.Tests;

[Collection("Neo4j")]
public class ParameterizedQueryTests(Neo4jFixture fixture)
{
    [Fact]
    public async Task Parameterized_query_finds_the_exact_named_person()
    {
        await using var session = fixture.Driver.AsyncSession();
        var expected = GraphSeeder.People[0];

        var name = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person {id: $id}) RETURN p.name AS name",
                new { id = expected.Id });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<string>(record["name"]);
        });

        name.Should().Be(expected.Name);
    }

    [Fact]
    public async Task Parameterized_query_treats_an_injection_shaped_value_as_an_inert_string()
    {
        await using var session = fixture.Driver.AsyncSession();
        var maliciousInput = "Nobody\" OR 1=1 //";

        var matchCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person) WHERE p.name = $name RETURN p.name AS name",
                new { name = maliciousInput });
            var records = await cursor.ToListAsync();
            return records.Count;
        });

        matchCount.Should().Be(0, "the payload doesn't match any real name and can't restructure the query");
    }

    [Fact]
    public async Task String_built_query_with_the_same_input_leaks_every_person()
    {
        await using var session = fixture.Driver.AsyncSession();
        var maliciousInput = "Nobody\" OR 1=1 //";

        // The vulnerable counterpart to the test above: concatenating the
        // same value into the query text turns "OR 1=1" into a
        // tautology, so the WHERE clause matches every row instead of none.
        var unsafeQuery = $"MATCH (p:Person) WHERE p.name = \"{maliciousInput}\" RETURN p.name AS name";

        var matchCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(unsafeQuery);
            var records = await cursor.ToListAsync();
            return records.Count;
        });

        matchCount.Should().Be(GraphSeeder.PersonCount, "string-built Cypher lets the payload rewrite the WHERE clause");
    }

    [Fact]
    public async Task Parameterized_query_compares_values_by_type_not_just_text()
    {
        await using var session = fixture.Driver.AsyncSession();

        // No person is literally named "123" -- a numeric-looking string
        // parameter must not accidentally match anything via loose
        // comparison.
        var matchCount = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person) WHERE p.name = $name RETURN p.name AS name",
                new { name = "123" });
            var records = await cursor.ToListAsync();
            return records.Count;
        });

        matchCount.Should().Be(0);
    }

    [Fact]
    public async Task Multiple_parameters_combine_correctly_in_one_query()
    {
        await using var session = fixture.Driver.AsyncSession();
        var edge = GraphSeeder.WorksAtEdges[0];

        var matched = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person {id: $personId})-[r:WORKS_AT]->(c:Company {id: $companyId})
                WHERE r.role = $role
                RETURN count(r) AS total
                """,
                new { personId = edge.PersonId, companyId = edge.CompanyId, role = edge.Role });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<long>(record["total"]);
        });

        matched.Should().Be(1);
    }
}
