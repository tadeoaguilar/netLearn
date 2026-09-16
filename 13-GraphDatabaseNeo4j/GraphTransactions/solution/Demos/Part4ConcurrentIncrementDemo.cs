using Neo4j.Driver;

namespace GraphTransactions.Demos;

public static class Part4ConcurrentIncrementDemo
{
    public static async Task RunAsync(IDriver driver)
    {
        var personId = $"person-{Guid.NewGuid()}";
        await using (var setupSession = driver.AsyncSession())
        {
            await setupSession.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    "CREATE (:Person {id: $personId, name: 'Concurrent Target', referralCount: 0})",
                    new { personId });
            });
        }

        Console.WriteLine("Starting two concurrent transactions against the SAME Person node...");

        // Writer A holds its lock for 300ms after its SET before committing.
        // Writer B starts at the same instant with no delay, so whichever
        // one loses the race for the node's write lock has to genuinely
        // BLOCK and wait -- this is a real overlapping race, not two
        // sequential writes dressed up to look concurrent.
        var taskA = IncrementWithDelayAsync(driver, personId, "Writer A", delayBeforeCommitMs: 300);
        var taskB = IncrementWithDelayAsync(driver, personId, "Writer B", delayBeforeCommitMs: 0);

        await Task.WhenAll(taskA, taskB);

        await using var readSession = driver.AsyncSession();
        var finalCount = await readSession.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (p:Person {id: $personId}) RETURN p.referralCount AS referralCount",
                new { personId });
            var record = await cursor.SingleAsync();
            return record["referralCount"].As<int>();
        });

        Console.WriteLine($"Final referralCount: {finalCount} (expected 2 -- BOTH increments landed, no lost update).");
        Console.WriteLine();
        Console.WriteLine("Contrast with modules 10/11's OPTIMISTIC concurrency (xmin / _etag): there,");
        Console.WriteLine("both writers would read the same starting value, and whichever wrote SECOND");
        Console.WriteLine("would be rejected (DbUpdateConcurrencyException / 412 PreconditionFailed) and");
        Console.WriteLine("have to reload-and-retry. Here, Neo4j's default is closer to PESSIMISTIC");
        Console.WriteLine("locking: the first writer to touch the node inside a transaction takes a lock");
        Console.WriteLine("on it, and the second writer's SET simply BLOCKS until the first commits or");
        Console.WriteLine("rolls back -- it doesn't fail, it waits, then applies on top of the now-current value.");
    }

    private static async Task IncrementWithDelayAsync(
        IDriver driver, string personId, string label, int delayBeforeCommitMs)
    {
        await using var session = driver.AsyncSession();
        await using var tx = await session.BeginTransactionAsync();
        try
        {
            // The write lock on this node is taken here, as soon as the SET
            // statement runs against it -- not at BeginTransactionAsync, and
            // not at CommitAsync.
            await tx.RunAsync(
                "MATCH (p:Person {id: $personId}) SET p.referralCount = p.referralCount + 1",
                new { personId });

            if (delayBeforeCommitMs > 0)
            {
                Console.WriteLine($"{label}: holding the lock for {delayBeforeCommitMs}ms before committing...");
                await Task.Delay(delayBeforeCommitMs);
            }

            await tx.CommitAsync();
            Console.WriteLine($"{label}: committed.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
