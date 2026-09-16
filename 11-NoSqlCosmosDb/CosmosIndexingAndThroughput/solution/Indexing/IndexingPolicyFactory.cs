using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Indexing;

// Builds the custom indexing policy used for Part 2 of the exercise:
// exclude the orderLines subtree (never queried into directly, but present
// on every order, so indexing it costs RU on every single write) and add a
// composite index for the one ORDER BY this app actually issues on two
// properties at once.
public static class IndexingPolicyFactory
{
    public static IndexingPolicy CreateOrdersIndexingPolicy()
    {
        var policy = new IndexingPolicy
        {
            IndexingMode = IndexingMode.Consistent,
            Automatic = true,
        };

        policy.IncludedPaths.Add(new IncludedPath { Path = "/*" });

        // orderLines is an embedded array that this app never filters or
        // projects on directly -- excluding it stops every write from
        // paying to maintain an index term per array element.
        policy.ExcludedPaths.Add(new ExcludedPath { Path = "/orderLines/*" });
        policy.ExcludedPaths.Add(new ExcludedPath { Path = "/\"_etag\"/?" });

        // Cosmos can only sort by two properties in one query if a matching
        // composite index exists -- this is what makes
        // "ORDER BY customerId ASC, orderDate DESC" possible.
        policy.CompositeIndexes.Add(new Collection<CompositePath>
        {
            new() { Path = "/customerId", Order = CompositePathSortOrder.Ascending },
            new() { Path = "/orderDate", Order = CompositePathSortOrder.Descending },
        });

        return policy;
    }
}
