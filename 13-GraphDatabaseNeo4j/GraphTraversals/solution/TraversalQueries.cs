using Neo4j.Driver;

namespace GraphTraversals;

/// <summary>
/// The traversal queries this module's EXERCISE.md builds up part by part,
/// all against the KNOWS/FOLLOWS graph <see cref="GraphSeeder"/> creates.
/// </summary>
public sealed class TraversalQueries
{
    // Variable-length relationship bounds (the *1..N part of a pattern)
    // must be integer LITERALS in Cypher -- Neo4j does not allow a query
    // parameter there. That's why every method that takes a maxHops/depth
    // argument below builds the query text with string interpolation
    // instead of passing it as a $parameter like every other value here.
    // The interpolated value is a validated .NET int, never raw user text,
    // so this isn't the injection risk that string-building a WHERE clause
    // from a text parameter would be.
    private const int MaxAllowedHops = 15;

    private readonly IDriver _driver;

    public TraversalQueries(IDriver driver)
    {
        _driver = driver;
    }

    /// <summary>
    /// Part 1: the shortest KNOWS path between two people, as the ordered
    /// list of person ids along that path (including both endpoints), not
    /// just "yes/no, they're connected" or the hop count.
    /// </summary>
    public async Task<IReadOnlyList<string>> ShortestPathAsync(
        string fromId, string toId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $fromId}), (b:Person {id: $toId})
                MATCH p = shortestPath((a)-[:KNOWS*]-(b))
                RETURN [n IN nodes(p) | n.id] AS ids
                """,
                new { fromId, toId });

            if (!await cursor.FetchAsync())
            {
                return new List<string>();
            }

            return cursor.Current["ids"].As<List<string>>();
        });
    }

    /// <summary>
    /// Part 2: every shortest KNOWS path between two people, when more than
    /// one exists at the same (minimal) length -- e.g. "show every shortest
    /// introduction path," not an arbitrary single one.
    /// </summary>
    public async Task<IReadOnlyList<IReadOnlyList<string>>> AllShortestPathsAsync(
        string fromId, string toId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $fromId}), (b:Person {id: $toId})
                MATCH p = allShortestPaths((a)-[:KNOWS*]-(b))
                RETURN [n IN nodes(p) | n.id] AS ids
                """,
                new { fromId, toId });

            var paths = new List<IReadOnlyList<string>>();
            while (await cursor.FetchAsync())
            {
                paths.Add(cursor.Current["ids"].As<List<string>>());
            }

            return paths;
        });
    }

    /// <summary>
    /// Part 3: everyone reachable from a person within a bounded number of
    /// KNOWS hops (1..maxHops). Bounding the pattern keeps this cheap on a
    /// large graph -- an unbounded -[:KNOWS*]- traversal has to explore
    /// every path to every reachable node.
    /// </summary>
    public async Task<IReadOnlyList<string>> WithinHopsAsync(
        string startId, int maxHops, CancellationToken cancellationToken = default)
    {
        ValidateHops(maxHops);

        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            // Raw string literal interpolation ($"""...""") has brace-counting
            // rules that fight with Cypher's own {property: $param} syntax, so
            // this query is built as a regular verbatim interpolated string
            // instead: {{ and }} are literal braces, {maxHops} interpolates.
            var cursor = await tx.RunAsync(
                $@"
                MATCH (start:Person {{id: $startId}})-[:KNOWS*1..{maxHops}]-(other:Person)
                RETURN DISTINCT other.id AS id
                ",
                new { startId });

            var ids = new List<string>();
            while (await cursor.FetchAsync())
            {
                ids.Add(cursor.Current["id"].As<string>());
            }

            return ids;
        });
    }

    /// <summary>
    /// Part 4: "friends in common" -- people who KNOW both a and b, without
    /// caring whether a and b know each other directly.
    /// </summary>
    public async Task<IReadOnlyList<string>> MutualConnectionsAsync(
        string aId, string bId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Person {id: $aId})-[:KNOWS]-(mutual:Person)-[:KNOWS]-(b:Person {id: $bId})
                WHERE mutual.id <> $aId AND mutual.id <> $bId
                RETURN DISTINCT mutual.id AS id
                ORDER BY id
                """,
                new { aId, bId });

            var ids = new List<string>();
            while (await cursor.FetchAsync())
            {
                ids.Add(cursor.Current["id"].As<string>());
            }

            return ids;
        });
    }

    /// <summary>
    /// Part 5: degrees of separation -- everyone reachable within maxHops
    /// KNOWS hops of start, grouped by the exact (minimum) hop distance at
    /// which each person is first reached.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> DegreesOfSeparationAsync(
        string startId, int maxHops, CancellationToken cancellationToken = default)
    {
        ValidateHops(maxHops);

        await using var session = _driver.AsyncSession();

        var byHop = await session.ExecuteReadAsync(async tx =>
        {
            // Same reason as WithinHopsAsync above: a regular verbatim
            // interpolated string, not a raw string literal, so {{ }} can mean
            // "literal brace" and {maxHops} can mean "interpolate" without a
            // brace-counting conflict.
            var cursor = await tx.RunAsync(
                $@"
                MATCH p = (start:Person {{id: $startId}})-[:KNOWS*1..{maxHops}]-(other:Person)
                WITH other, min(length(p)) AS hops
                RETURN other.id AS id, hops
                ORDER BY hops, id
                ",
                new { startId });

            var results = new List<(string Id, int Hops)>();
            while (await cursor.FetchAsync())
            {
                results.Add((cursor.Current["id"].As<string>(), cursor.Current["hops"].As<int>()));
            }

            return results;
        });

        return byHop
            .GroupBy(r => r.Hops)
            .ToDictionary(
                g => g.Key,
                IReadOnlyList<string> (g) => g.Select(r => r.Id).ToList());
    }

    /// <summary>Directed FOLLOWS: who follows this person.</summary>
    public async Task<IReadOnlyList<string>> FollowersAsync(
        string personId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (follower:Person)-[:FOLLOWS]->(p:Person {id: $personId})
                RETURN follower.id AS id
                ORDER BY id
                """,
                new { personId });

            var ids = new List<string>();
            while (await cursor.FetchAsync())
            {
                ids.Add(cursor.Current["id"].As<string>());
            }

            return ids;
        });
    }

    /// <summary>Directed FOLLOWS: who this person follows.</summary>
    public async Task<IReadOnlyList<string>> FollowingAsync(
        string personId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (p:Person {id: $personId})-[:FOLLOWS]->(followee:Person)
                RETURN followee.id AS id
                ORDER BY id
                """,
                new { personId });

            var ids = new List<string>();
            while (await cursor.FetchAsync())
            {
                ids.Add(cursor.Current["id"].As<string>());
            }

            return ids;
        });
    }

    private static void ValidateHops(int maxHops)
    {
        if (maxHops is < 1 or > MaxAllowedHops)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxHops), maxHops, $"maxHops must be between 1 and {MaxAllowedHops}.");
        }
    }
}
