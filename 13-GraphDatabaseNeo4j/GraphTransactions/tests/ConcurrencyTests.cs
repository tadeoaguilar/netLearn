using Neo4j.Driver;

namespace GraphTransactions.Tests;

[Collection("Neo4j")]
public class ConcurrencyTests
{
    private readonly IDriver _driver;

    public ConcurrencyTests(Neo4jFixture fixture) => _driver = fixture.Driver;

    [Fact]
    public async Task Two_concurrent_transactions_incrementing_the_same_node_both_apply_no_lost_update()
    {
        var personId = $"person-{Guid.NewGuid()}";
        await GraphTestHelpers.CreatePersonAsync(_driver, personId, "Concurrent Target");

        // Writer A holds the node's write lock for 300ms after its SET
        // before committing; Writer B starts at the same instant with no
        // delay, via Task.WhenAll, so its SET call genuinely has to block on
        // A's lock for at least part of that window -- a real overlap, not
        // two sequential writes pretending to race.
        var taskA = IncrementAsync(personId, delayBeforeCommitMs: 300);
        var taskB = IncrementAsync(personId, delayBeforeCommitMs: 0);

        await Task.WhenAll(taskA, taskB);

        var finalCount = await GraphTestHelpers.GetReferralCountAsync(_driver, personId);
        finalCount.Should().Be(2, "Neo4j's node-level write lock makes the second writer WAIT, not overwrite");
    }

    [Fact]
    public async Task Four_concurrent_transactions_incrementing_the_same_node_all_apply()
    {
        var personId = $"person-{Guid.NewGuid()}";
        await GraphTestHelpers.CreatePersonAsync(_driver, personId, "Concurrent Target 2");

        var tasks = Enumerable.Range(0, 4).Select(_ => IncrementAsync(personId, delayBeforeCommitMs: 50));
        await Task.WhenAll(tasks);

        var finalCount = await GraphTestHelpers.GetReferralCountAsync(_driver, personId);
        finalCount.Should().Be(4, "all four concurrent writers must apply -- none can be silently lost");
    }

    private async Task IncrementAsync(string personId, int delayBeforeCommitMs)
    {
        await using var session = _driver.AsyncSession();
        await using var tx = await session.BeginTransactionAsync();
        try
        {
            await tx.RunAsync(
                "MATCH (p:Person {id: $personId}) SET p.referralCount = p.referralCount + 1",
                new { personId });

            if (delayBeforeCommitMs > 0)
            {
                await Task.Delay(delayBeforeCommitMs);
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
