# Exercise: Reacting to Changes with the Cosmos DB Change Feed

## Overview
In this exercise, you'll build a **materialized read model** on top of Cosmos DB using the **change feed processor**. You'll write `Order` documents into an `orders` container and watch a background processor keep a `customerordersummary` document up to date for each customer -- without your application code ever running a live aggregation query.

## Learning Goals
By completing this exercise, you will:
- Understand what the change feed is (and is NOT) as a NoSQL analog to a relational trigger
- Set up a change feed processor with a lease container
- Build a materialized read model that's updated asynchronously as data changes
- Understand at-least-once delivery and design an idempotent change handler
- Know what happens when a change handler throws, and where you'd add retry/dead-letter logic

**Heads up on pacing:** this exercise takes longer to *see results* than the module's other projects. Writing an `Order` document does not synchronously update the summary -- the processor has to notice the change, and that depends on lease acquisition (can take a couple of seconds on a cold start) and the processor's poll interval. Budget for polling and waiting, not instant feedback.

---

## The Scenario

You're building the read side of a retail order system. Writes go to an `orders` container, one document per order, partitioned by `/customerId`. But a very common read -- "how many orders has this customer placed, and how much have they spent in total?" -- would otherwise require a cross-partition (or at least a full-partition) aggregation query every single time someone views a customer's profile.

Instead, you'll maintain a `CustomerOrderSummary` document per customer, kept fresh by a change feed processor that watches `orders` and recomputes the summary whenever something changes.

---

## Part 1: What the Change Feed Is (and Isn't)

The Cosmos DB change feed is a **persistent, ordered-per-partition log** of inserts and updates to the items in a container. Every write shows up in the feed for that item's partition, in the order it happened relative to other writes in that same partition (there is no cross-partition ordering guarantee).

It is often described as "Cosmos DB's version of a database trigger," and that's a useful starting intuition -- but the differences matter:

| | Relational trigger | Cosmos DB change feed |
|---|---|---|
| Timing | Synchronous, part of the same transaction | Asynchronous -- a separate process reads the feed later |
| Guarantee | Exactly once, transactional | At-least-once, eventually |
| Scope | Fires on the write itself | A separate reader polls for changes |
| Deletes | Fires on DELETE | **Deletes are NOT in the feed** (unless you soft-delete with a flag and treat that as an update) |

That last row is a real, sharp-edged limitation: if your application hard-deletes an `Order` document, the change feed never sees it, and anything you built on the feed (like this exercise's summary) will not react to the deletion. Production systems that need deletes to be visible in the feed almost always model deletion as a soft-delete update (e.g. `IsDeleted = true`) precisely so it's a change the feed does carry.

**Your Task:**
No code yet. Before writing anything, make sure you can answer:

**Questions to think about:**
1. Why might "eventually consistent, asynchronous" be an acceptable trade-off for a read model like `CustomerOrderSummary`, but not for something like a payment authorization?
2. If you hard-delete an `Order`, what happens to that customer's summary? What would you have to do differently to make deletions visible to a change feed consumer?

---

## Part 2: Setting Up the Change Feed Processor

The change feed processor is the SDK's built-in way to consume the feed: you hand it a delegate, and it calls your delegate with batches of changed items as they arrive, checkpointing its progress as it goes.

**Your Task:**
Add the domain models. Create `Models.cs`:

```csharp
using Newtonsoft.Json;

namespace CosmosChangeFeed;

public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public DateTimeOffset OrderDate { get; set; }

    public string Status { get; set; } = "Placed";

    public List<OrderLine> OrderLines { get; set; } = new();

    public decimal TotalAmount { get; set; }
}

public class OrderLine
{
    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

public class CustomerOrderSummary
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public int TotalOrders { get; set; }

    public decimal TotalSpent { get; set; }

    public DateTimeOffset? LastOrderDate { get; set; }
}
```

Notice `CustomerOrderSummary.Id` is going to hold the *customer id*, not a random GUID -- there's exactly one summary document per customer, and using a predictable id turns the handler's write into a cheap point read/replace instead of a query.

Now wire up the processor. In `Program.cs`:

```csharp
var ordersContainer = database.GetContainer("orders");
var summaryContainer = database.GetContainer("customerordersummary");
var leaseContainer = database.GetContainer("leases");

var processor = ordersContainer
    .GetChangeFeedProcessorBuilder<Order>("orderProcessor", HandleChangesAsync)
    .WithInstanceName($"host-{Environment.MachineName}-{Environment.ProcessId}")
    .WithLeaseContainer(leaseContainer)
    .Build();

await processor.StartAsync();

// ... do work while the processor runs in the background ...

await processor.StopAsync();

async Task HandleChangesAsync(IReadOnlyCollection<Order> changes, CancellationToken cancellationToken)
{
    foreach (var order in changes)
    {
        Console.WriteLine($"Changed: order {order.Id} for customer {order.CustomerId}");
    }
}
```

**Key Concepts:**
- **"orderProcessor"** is the *processor name*. It's baked into the lease documents the processor writes -- change it and you start a brand-new lease set, meaning you'll reprocess the container's entire history from the beginning next time you start.
- **The lease container** (`leases`) is where the processor tracks, per partition, how far it has read. This is what lets processing **resume after a restart** instead of starting over, and it's also the mechanism that lets you run **multiple processor instances** with the same processor name: they'll split the container's partitions between themselves by racing for leases, and each partition is owned by exactly one instance at a time.
- **`WithInstanceName`** identifies *this* running process among any others sharing the same processor name. It doesn't need to be globally unique forever, just unique among instances running concurrently.

**Why:** without a lease container, there is nothing to remember where the processor left off -- every restart would mean reprocessing the whole feed from the start (or, if you don't want that, you'd need to build your own progress tracking, which is exactly what the lease container already does for you).

**Questions to think about:**
1. What would happen if two processor instances used the same instance name?
2. If you deploy a second, independent consumer that needs to process the same feed for a *different* purpose (say, a notifications service), should it share the `orderProcessor` processor name and lease container, or use its own?

---

## Part 3: Building the Materialized Read Model

Now make the handler actually do something: keep `CustomerOrderSummary` up to date.

**Your Task:**
Create `OrderChangeHandler.cs`:

```csharp
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmosChangeFeed;

public class OrderChangeHandler
{
    private readonly Container _ordersContainer;
    private readonly Container _summaryContainer;

    public OrderChangeHandler(Container ordersContainer, Container summaryContainer)
    {
        _ordersContainer = ordersContainer;
        _summaryContainer = summaryContainer;
    }

    public async Task HandleChangesAsync(IReadOnlyCollection<Order> changes, CancellationToken cancellationToken)
    {
        var affectedCustomerIds = changes.Select(o => o.CustomerId).Distinct();

        foreach (var customerId in affectedCustomerIds)
        {
            await RecomputeSummaryAsync(customerId, cancellationToken);
        }
    }

    private async Task RecomputeSummaryAsync(string customerId, CancellationToken cancellationToken)
    {
        var query = _ordersContainer.GetItemLinqQueryable<Order>(
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(customerId) })
            .Where(o => o.CustomerId == customerId);

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

        await _summaryContainer.UpsertItemAsync(summary, new PartitionKey(customerId), cancellationToken: cancellationToken);
    }
}
```

Wire it into `Program.cs` in place of the `Console.WriteLine` handler from Part 2:

```csharp
var handler = new OrderChangeHandler(ordersContainer, summaryContainer);

var processor = ordersContainer
    .GetChangeFeedProcessorBuilder<Order>("orderProcessor", handler.HandleChangesAsync)
    .WithInstanceName($"host-{Environment.MachineName}-{Environment.ProcessId}")
    .WithLeaseContainer(leaseContainer)
    .Build();
```

**Why this is the payoff:** `Order` and `CustomerOrderSummary` share the same partition key value (the customer id), so `RecomputeSummaryAsync`'s query never fans out across the container -- it's a single-partition query, as cheap as Cosmos queries get. Now reading a customer's totals is a single point read of `customerordersummary` by id: no aggregation, no fan-out, no matter how many orders that customer has ever placed.

**Questions to think about:**
1. Why does using the customer id as `CustomerOrderSummary.Id` (rather than a generated GUID) matter here, beyond convenience?
2. This handler queries `orders` once per *distinct* customer in the batch, not once per changed order. Why does that matter when a customer places several orders in quick succession?

---

## Part 4: At-Least-Once Delivery and Idempotency

The change feed guarantees **at-least-once** delivery, not exactly-once. If the processor's handler completes but the process crashes before the processor checkpoints its lease, the *same batch* is redelivered to a new instance after restart. Your handler **must** cope with seeing the same change more than once.

**Your Task:**
Look back at `RecomputeSummaryAsync` from Part 3. Notice it doesn't touch a running total at all -- every time it runs, it throws away whatever `CustomerOrderSummary` currently says and recomputes `TotalOrders`, `TotalSpent`, and `LastOrderDate` from scratch by querying `orders` directly. That's what makes it idempotent: the result depends only on the current contents of the `orders` partition, never on how many times the handler has already run for that customer. Handle the same change once, twice, or ten times -- the summary converges to the same answer every time.

Now consider the tempting alternative -- and why it's a trap:

```csharp
// DO NOT DO THIS -- shown to illustrate the failure mode, not to use.
private async Task IncrementSummaryAsync(Order order, CancellationToken ct)
{
    CustomerOrderSummary summary;
    try
    {
        var response = await _summaryContainer.ReadItemAsync<CustomerOrderSummary>(
            order.CustomerId, new PartitionKey(order.CustomerId), cancellationToken: ct);
        summary = response.Resource;
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        summary = new CustomerOrderSummary { Id = order.CustomerId, CustomerId = order.CustomerId };
    }

    summary.TotalOrders++;                      // <-- redelivered? counted twice.
    summary.TotalSpent += order.TotalAmount;     // <-- redelivered? added twice.
    summary.LastOrderDate = order.OrderDate;

    await _summaryContainer.UpsertItemAsync(summary, new PartitionKey(order.CustomerId), cancellationToken: ct);
}
```

If this handler is invoked twice for the same order (a redelivery), `TotalOrders` and `TotalSpent` both silently inflate. There's no error, no exception -- just a quietly wrong number that's very easy to miss in testing and very annoying to explain in production.

If you ever need the increment approach anyway (for instance, because recomputing from source is too expensive), you'd have to add your own deduplication: record each processed order id somewhere (on the summary document itself, or in a separate collection) and skip any order id you've already applied. That's real, ongoing bookkeeping cost, on every single event, forever. Compare that to `RecomputeSummaryAsync`'s approach, which pays a single-partition query per batch and gets idempotency for free.

**Questions to think about:**
1. Under what circumstances would recomputing from source data become *too expensive* to do on every change, forcing you toward the increment-plus-dedup approach?
2. If you did need to track processed order ids for deduplication, where would you store that list, and how would you keep it from growing forever?

---

## Part 5: Handling Processor Failures

What happens if `HandleChangesAsync` throws an exception partway through processing a batch?

**Your Task:**
No code to write for this part -- read, then answer the questions below.

The change feed processor's default behavior when your handler throws is to **not** silently retry the batch forever, and it does **not** automatically checkpoint that batch as processed. Depending on how you've configured the processor, an unhandled exception from your delegate can bring the processor host down (it propagates out of the internal loop), or -- if you're using error-handling hooks the SDK exposes on newer processor builders -- get reported to a handler you provide. Either way, the SDK does not include built-in dead-lettering: there's no default "send the poison batch somewhere else and move on" behavior.

That means the responsibility for retry policy and dead-lettering is yours:
- **Transient failures** (a momentary timeout talking to `customerordersummary`) are usually fine to retry a few times with backoff *inside* your handler, before letting the exception propagate.
- **Poison batches** -- a change that will *never* succeed no matter how many times you retry it (e.g. a malformed document that fails deserialization) -- need somewhere else to go, or they'll block the processor from making progress on that partition forever. A common pattern is catching the failure, writing the problem batch (or its identifying info) to a separate "dead letter" container or queue, and letting the processor move on rather than getting stuck retrying the same unprocessable batch indefinitely.

**Questions to think about:**
1. If `RecomputeSummaryAsync` throws because `customerordersummary` is briefly unavailable, what happens to the *next* batch the processor tries to deliver -- does it wait, or does it move on and leave that customer's summary stale?
2. Where would you put a retry-with-backoff policy: inside `HandleChangesAsync` itself, or wrapped around the call to `processor.StartAsync()`? Why?

---

## Part 6: Put It Together

**Your Task:**
Finish `Program.cs` so that it:
1. Builds and starts the processor (Parts 2-3)
2. Writes two `Order` documents for the same customer
3. Polls `customerordersummary` for that customer until `TotalOrders` reaches 2 (or a timeout elapses) -- **do not** use a fixed `Task.Delay` and assume the summary is ready; poll
4. Prints the resulting summary
5. Stops the processor cleanly with `await processor.StopAsync()`

Run it against a locally started emulator (see `GETTING_STARTED.md`), or via the AppHost:

```bash
dotnet run --project AppHost
```

---

## Reflection Questions

After completing this exercise, answer these:

1. **Change feed vs. trigger.** Name two concrete ways the change feed behaves differently from a relational trigger, beyond "it's async."
2. **Deletes.** Why doesn't the change feed carry deletes, and what would you change about your data model if a consumer of your feed needed to react to deletions?
3. **Idempotency.** In your own words, why is "recompute from source" naturally idempotent while "increment a counter" is not?
4. **Lease container.** What two distinct jobs does the lease container do (think: restarts, and multiple instances)?
5. **Failure handling.** If your handler threw on every single batch for a given customer's partition, what would you observe happening to that customer's summary, and to the rest of the container's partitions?

---

## Summary

You've learned:
- What the change feed is (an ordered-per-partition log of inserts/updates) and its real limitations (no deletes, at-least-once, asynchronous)
- How to set up a change feed processor with a lease container, and what the lease container is for
- How to build a materialized read model that stays fresh without live aggregation queries
- Why recompute-from-source is a naturally idempotent design, and why increment-based aggregation is not
- What happens when a change handler throws, and where retry/dead-letter logic belongs

## Next Steps

This project sits alongside the other `11-NoSqlCosmosDb` projects covering Cosmos DB modeling, partitioning, and querying. Compare this materialized-view approach to a live aggregation query over the same data -- which would you reach for, and when?

---

**Happy Learning!**
