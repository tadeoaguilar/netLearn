namespace GraphTransactions.Demos;

// There's no new API here -- Part 3 is a lesson, not a demo. It exists as
// its own file so `dotnet run -- 3` can print the comparison on demand,
// and so EXERCISE.md has a concrete artifact to point at.
public static class Part3CrossDatabaseContrastDemo
{
    public static Task RunAsync()
    {
        Console.WriteLine("Cosmos DB (11-NoSqlCosmosDb/CosmosConsistencyAndTransactions):");
        Console.WriteLine("  TransactionalBatch only guarantees atomicity for operations against");
        Console.WriteLine("  documents that share BOTH a container and a partition key value. That");
        Console.WriteLine("  project had to invent CustomerLoyaltyProfile -- a denormalized document");
        Console.WriteLine("  copied INTO the orders container, under the SAME partition key as the");
        Console.WriteLine("  order -- purely so an order-plus-loyalty-points write could be batched");
        Console.WriteLine("  atomically. Redesigning the data model was the price of atomicity.");
        Console.WriteLine();
        Console.WriteLine("Neo4j (this project, Part 2):");
        Console.WriteLine("  The referral transaction touches a Company node, the new hire's Person");
        Console.WriteLine("  node, AND the referrer's Person node -- three different graph elements");
        Console.WriteLine("  that, modeled in Cosmos DB with Person partitioned by /id and Company by");
        Console.WriteLine("  /id, would very likely sit in three different partitions. Neo4j needed no");
        Console.WriteLine("  denormalization to make that transaction atomic -- it's just a");
        Console.WriteLine("  transaction. BeginTransactionAsync/CommitAsync/RollbackAsync don't care how");
        Console.WriteLine("  many nodes or relationships you touch, or what labels they carry, as long");
        Console.WriteLine("  as they live in the same database.");
        Console.WriteLine();
        Console.WriteLine("The trade-off is elsewhere, not eliminated: Cosmos buys horizontal,");
        Console.WriteLine("near-infinite scale by giving up cross-partition atomicity; Neo4j's");
        Console.WriteLine("single-database transactions assume the graph you're writing to fits on one");
        Console.WriteLine("machine (or one causal cluster), which is a real ceiling Cosmos doesn't have.");

        return Task.CompletedTask;
    }
}
