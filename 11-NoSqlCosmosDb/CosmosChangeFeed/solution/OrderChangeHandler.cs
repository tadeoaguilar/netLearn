using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmosChangeFeed;

/// <summary>
/// Handles a batch of changed "orders" documents delivered by the change
/// feed processor, keeping the "customerordersummary" read model in sync.
///
/// Idempotency by design: the change feed offers AT-LEAST-ONCE delivery. If
/// the processor crashes after processing a batch but before checkpointing
/// its lease, the same batch is redelivered after restart. This handler
/// copes by RECOMPUTING each affected customer's summary from that
/// customer's own orders, rather than incrementing counters on the existing
/// summary document. Recomputing from source data is naturally idempotent:
/// processing the same change once, twice, or ten times produces the exact
/// same summary, because the result only ever depends on the current state
/// of the "orders" partition, never on how many times this handler has run.
///
/// The alternative -- loading the existing summary and doing
/// `summary.TotalOrders++; summary.TotalSpent += order.TotalAmount;` -- is
/// NOT idempotent: a redelivered change would increment the counters a
/// second time and silently inflate the totals. Making that approach safe
/// requires extra bookkeeping (e.g. recording processed order ids on the
/// summary document, or in a separate dedup collection, and skipping any
/// order id already recorded there). That bookkeeping costs storage and an
/// extra check per event. Recomputing costs one point-read-by-partition
/// query per changed customer per batch. For this exercise -- and for most
/// real aggregates that are cheap to recompute from a single partition --
/// recompute-and-replace is the simpler, safer default. Reach for
/// increment-plus-dedup only when the source data needed to recompute is
/// expensive or no longer available (e.g. it was itself deleted or archived).
/// </summary>
public class OrderChangeHandler
{
    private readonly Container _ordersContainer;
    private readonly Container _summaryContainer;

    public OrderChangeHandler(Container ordersContainer, Container summaryContainer)
    {
        _ordersContainer = ordersContainer;
        _summaryContainer = summaryContainer;
    }

    /// <summary>
    /// Matches the <c>ChangesHandler&lt;Order&gt;</c> delegate shape expected
    /// by <c>GetChangeFeedProcessorBuilder&lt;Order&gt;</c>.
    /// </summary>
    public async Task HandleChangesAsync(IReadOnlyCollection<Order> changes, CancellationToken cancellationToken)
    {
        // A single batch can contain several changes for the same customer
        // (e.g. two orders placed back to back before the processor's next
        // poll). Recomputing once per distinct customer per batch avoids
        // redundant work.
        var affectedCustomerIds = changes
            .Select(order => order.CustomerId)
            .Distinct();

        foreach (var customerId in affectedCustomerIds)
        {
            await RecomputeSummaryAsync(customerId, cancellationToken);
        }
    }

    private async Task RecomputeSummaryAsync(string customerId, CancellationToken cancellationToken)
    {
        // Order and CustomerOrderSummary share the same partition key value
        // (the customer id), so this is a single-partition query -- cheap,
        // and it never fans out across the container.
        var query = _ordersContainer.GetItemLinqQueryable<Order>(
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(customerId) })
            .Where(order => order.CustomerId == customerId);

        var totalOrders = 0;
        var totalSpent = 0m;
        DateTimeOffset? lastOrderDate = null;

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            foreach (var order in await iterator.ReadNextAsync(cancellationToken))
            {
                totalOrders++;
                totalSpent += order.TotalAmount;
                if (lastOrderDate is null || order.OrderDate > lastOrderDate)
                {
                    lastOrderDate = order.OrderDate;
                }
            }
        }

        var summary = new CustomerOrderSummary
        {
            Id = customerId,
            CustomerId = customerId,
            TotalOrders = totalOrders,
            TotalSpent = totalSpent,
            LastOrderDate = lastOrderDate,
        };

        // Upsert, not a plain create: this is a redelivery-safe replace of
        // whatever summary document currently exists for this customer.
        await _summaryContainer.UpsertItemAsync(
            summary,
            new PartitionKey(customerId),
            cancellationToken: cancellationToken);
    }
}
