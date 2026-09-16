using GraphQuerying.Seed;
using Neo4j.Driver;

namespace GraphQuerying.Tests;

[Collection("Neo4j")]
public class AggregationAndPaginationTests(Neo4jFixture fixture)
{
    [Fact]
    public async Task Count_of_all_person_nodes_matches_the_seed_data()
    {
        await using var session = fixture.Driver.AsyncSession();

        var total = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:Person) RETURN count(p) AS total");
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<long>(record["total"]);
        });

        total.Should().Be(GraphSeeder.PersonCount);
    }

    [Fact]
    public async Task Grouping_employees_per_company_matches_seed_data_exactly()
    {
        await using var session = fixture.Driver.AsyncSession();

        var expected = GraphSeeder.WorksAtEdges
            .GroupBy(e => e.CompanyId)
            .ToDictionary(
                g => GraphSeeder.Companies.First(c => c.Id == g.Key).Name,
                g => (long)g.Count());

        var actual = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person)-[:WORKS_AT]->(c:Company)
                RETURN c.name AS company, count(p) AS employees
                """);
            var records = await cursor.ToListAsync();
            return records.ToDictionary(
                r => Neo4j.Driver.ValueExtensions.As<string>(r["company"]),
                r => Neo4j.Driver.ValueExtensions.As<long>(r["employees"]));
        });

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Collect_gathers_every_KNOWS_neighbor_name()
    {
        await using var session = fixture.Driver.AsyncSession();
        var personId = GraphSeeder.People[1].Id;

        var expectedIds = new HashSet<string>();
        foreach (var edge in GraphSeeder.KnowsEdges)
        {
            if (edge.PersonAId == personId)
            {
                expectedIds.Add(edge.PersonBId);
            }
            else if (edge.PersonBId == personId)
            {
                expectedIds.Add(edge.PersonAId);
            }
        }

        var expectedNames = expectedIds
            .Select(id => GraphSeeder.People.First(p => p.Id == id).Name)
            .ToList();

        var actualNames = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (:Person {id: $id})-[:KNOWS]-(other:Person) RETURN collect(other.name) AS names",
                new { id = personId });
            var record = await cursor.SingleAsync();
            return Neo4j.Driver.ValueExtensions.As<List<string>>(record["names"]);
        });

        actualNames.Should().BeEquivalentTo(expectedNames);
    }

    [Fact]
    public async Task Pagination_returns_the_correct_slice_for_a_middle_page()
    {
        await using var session = fixture.Driver.AsyncSession();
        const int pageSize = 20;
        const int page = 1;

        var expectedNames = GraphSeeder.People
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => p.Name)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToList();

        var actualNames = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person)
                WITH p
                ORDER BY p.name, p.id
                SKIP $skip LIMIT $limit
                RETURN p.name AS name
                """,
                new { skip = page * pageSize, limit = pageSize });
            var records = await cursor.ToListAsync();
            return records.Select(r => Neo4j.Driver.ValueExtensions.As<string>(r["name"])).ToList();
        });

        actualNames.Should().Equal(expectedNames);
    }

    [Fact]
    public async Task Last_page_returns_the_remainder_when_the_count_does_not_divide_evenly()
    {
        await using var session = fixture.Driver.AsyncSession();
        const int pageSize = 20;
        var lastPage = (GraphSeeder.PersonCount - 1) / pageSize; // page 2, for 50 people / 20 per page

        var actualNames = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person)
                WITH p
                ORDER BY p.name, p.id
                SKIP $skip LIMIT $limit
                RETURN p.name AS name
                """,
                new { skip = lastPage * pageSize, limit = pageSize });
            var records = await cursor.ToListAsync();
            return records.Select(r => Neo4j.Driver.ValueExtensions.As<string>(r["name"])).ToList();
        });

        var expectedRemainder = GraphSeeder.PersonCount - lastPage * pageSize;
        actualNames.Should().HaveCount(expectedRemainder);
        actualNames.Count.Should().BeLessThan(pageSize);
    }

    [Fact]
    public async Task Every_page_together_covers_every_person_exactly_once()
    {
        await using var session = fixture.Driver.AsyncSession();
        const int pageSize = 20;
        var totalPages = (int)Math.Ceiling(GraphSeeder.PersonCount / (double)pageSize);

        var collected = new List<string>();
        for (var page = 0; page < totalPages; page++)
        {
            var pageNames = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(
                    """
                    MATCH (p:Person)
                    WITH p
                    ORDER BY p.name, p.id
                    SKIP $skip LIMIT $limit
                    RETURN p.name AS name
                    """,
                    new { skip = page * pageSize, limit = pageSize });
                var records = await cursor.ToListAsync();
                return records.Select(r => Neo4j.Driver.ValueExtensions.As<string>(r["name"])).ToList();
            });

            collected.AddRange(pageNames);
        }

        var expected = GraphSeeder.People
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => p.Name)
            .ToList();

        collected.Should().Equal(expected);
    }
}
