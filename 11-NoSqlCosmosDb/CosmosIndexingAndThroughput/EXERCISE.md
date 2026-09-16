# Exercise: Indexing and Throughput in Cosmos DB

## Overview
Every other module-11 exercise asks "how do I model this?" or "how do I query this?" This
one asks a different question: **what does any of that cost, and how do you control it?**
Cosmos DB bills you in Request Units (RUs) per operation, indexes every property by default
whether you asked it to or not, and lets you provision throughput two different ways with
very different cost/predictability trade-offs. None of that is visible unless you go
looking for it -- so this exercise is entirely about going looking for it, against the same
retail-orders domain (`Customer`, `Order` with embedded `OrderLine[]`) the rest of the
module uses, seeded with enough data (~100+ orders) that the cost differences are actually
visible instead of rounding to the same 1-2 RU on a handful of documents.

Everything here runs against the **real Cosmos DB emulator** via .NET Aspire -- RU cost and
indexing behavior aren't things an in-memory fake can teach you anything real about.

## Learning Goals
By completing this exercise, you will:
- Understand Cosmos DB's default indexing policy (everything indexed, automatically) and
  the write-cost implication of indexing a large embedded array you never query into
- Customize an indexing policy: exclude a subtree from indexing, and add a composite index
  to make a two-property `ORDER BY` possible at all
- Read `RequestCharge` off point reads, single- and cross-partition queries, and writes, to
  see the real relative cost of each shape of operation
- Compare manual (fixed RU/s) and autoscale (RU/s that scales between 10% and 100% of a
  configured max) throughput, and know which SDK calls configure each
- Handle `429 TooManyRequests` explicitly, and understand when that's redundant with what
  the SDK already retries for you and when it isn't

---

## The Scenario

Same two containers as the rest of the module:
- `customers` -- one document per customer, partitioned by `/id`
- `orders` -- one document per order, embedded `OrderLine[]`, partitioned by `/customerId`

Both containers and the emulator that hosts them are already wired up in
`AppHost/Program.cs`. Unlike the AppHost's container declarations elsewhere in this module,
notice that it does **not** configure an indexing policy or a throughput profile for either
container -- the Aspire Azure Cosmos DB hosting API's `AddContainer` only takes a name and a
partition key path, nothing else. Every indexing and throughput decision in this exercise is
made from the **client SDK side**, in your own startup code, against containers Aspire
already created with Cosmos's defaults. That's not a workaround -- it's also exactly how
you'd manage indexing policy and throughput against a container you don't want to
recreate every deployment.

---

## Part 0: Project Setup

**Your Task:**
In the `CosmosIndexingAndThroughput/` workspace project, create these empty folders to hold
your work: `Models/`, `Data/`, `Indexing/`, `Metrics/`, `Throughput/`, `Retry/`.

`appsettings.json` already points at `https://localhost:8081` with the emulator's
well-known default key. When you run via `AppHost`, Aspire overrides this connection string
with the one for the container it started; run standalone against a manually-started
emulator (see `GETTING_STARTED.md`) and this file's value is what's used.

---

## Part 1: Default Indexing

Cosmos DB indexes **every property of every document, automatically**, the moment you
create a container without specifying otherwise. There's no "add an index" step to get
started -- you get one whether you asked for it or not.

**Your Task:**
Create `Models/Customer.cs` and `Models/Order.cs`:

```csharp
// Models/Customer.cs
using Newtonsoft.Json;

namespace CosmosIndexingAndThroughput.Models;

public class Customer
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("city")]
    public string City { get; set; } = string.Empty;

    [JsonProperty("tier")]
    public string Tier { get; set; } = "Standard";
}
```

```csharp
// Models/Order.cs
using Newtonsoft.Json;

namespace CosmosIndexingAndThroughput.Models;

public class OrderLine
{
    [JsonProperty("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonProperty("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("unitPrice")]
    public decimal UnitPrice { get; set; }
}

public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("orderDate")]
    public DateTimeOffset OrderDate { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = "Pending";

    [JsonProperty("orderLines")]
    public List<OrderLine> OrderLines { get; set; } = new();

    [JsonProperty("totalAmount")]
    public decimal TotalAmount { get; set; }
}
```

**Your Task:**
Write a small seeder in `Data/DataSeeder.cs` that creates ~20 customers and ~120 orders
(1-4 `OrderLine`s each, drawn from a small hardcoded product catalog), upserting through
`Container.UpsertItemAsync`. Make it idempotent -- check `SELECT VALUE COUNT(1) FROM c`
first and skip seeding if the container already has enough documents. (Full reference:
[`solution/Data/DataSeeder.cs`](solution/Data/DataSeeder.cs).) Without this, every RU number
in the rest of the exercise is measuring a nearly-empty container, which hides exactly the
costs this exercise is trying to show you.

**Your Task:**
Create `Indexing/IndexingWalkthrough.cs` with a `RunPart1DefaultIndexingAsync` method that,
against the `orders` container (still on Cosmos's default indexing policy at this point):
1. Upserts a sample order and prints the write's `RequestCharge`.
2. Runs `SELECT * FROM o WHERE o.customerId = @customerId` and prints its `RequestCharge`.
3. Runs a query that reaches **into** `orderLines` --
   `SELECT VALUE o FROM o JOIN l IN o.orderLines WHERE l.productId = @productId` -- and
   prints its `RequestCharge` too.

**Why:** under the default policy, that `orderLines` query works, and works efficiently,
because Cosmos indexed every element of every order's `orderLines` array without being
asked. That sounds like a pure win until you look at the write side: **every single order
write pays to maintain an index term for every line item in that order**, even though this
app's actual query patterns (see the rest of this module) filter almost exclusively on
`customerId`, `status`, and `orderDate` -- never on a value buried inside `orderLines`. An
embedded array you never query into directly is pure write-cost overhead under the default
policy, with no corresponding read-side benefit you're actually using.

**Questions to think about:**
1. If this container held orders with 50-item carts instead of 1-4, what would you expect
   to happen to write RU cost under the default policy, and why?
2. Is there any query pattern where indexing `orderLines` by default *would* be worth its
   write cost in this domain?

---

## Part 2: Customizing the Indexing Policy

**Your Task:**
Create `Indexing/IndexingPolicyFactory.cs`:

```csharp
using System.Collections.ObjectModel;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Indexing;

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

        // Never queried into directly -- exclude it so writes stop paying to
        // index every element of a variable-length array.
        policy.ExcludedPaths.Add(new ExcludedPath { Path = "/orderLines/*" });
        policy.ExcludedPaths.Add(new ExcludedPath { Path = "/\"_etag\"/?" });

        // Required for `ORDER BY customerId, orderDate` -- Cosmos can only sort by two
        // properties at once if a matching composite index exists.
        policy.CompositeIndexes.Add(new Collection<CompositePath>
        {
            new() { Path = "/customerId", Order = CompositePathSortOrder.Ascending },
            new() { Path = "/orderDate", Order = CompositePathSortOrder.Descending },
        });

        return policy;
    }
}
```

**Your Task:**
Add a `RunPart2CustomIndexingPolicyAsync` method to `IndexingWalkthrough` that:
1. **Before** changing anything, attempts
   `SELECT * FROM o ORDER BY o.customerId ASC, o.orderDate DESC` and catches the
   `CosmosException` it throws. Print its `StatusCode` and message.
2. Reads the container's current properties with `ReadContainerAsync()`, sets
   `.IndexingPolicy` to `IndexingPolicyFactory.CreateOrdersIndexingPolicy()`, and applies it
   with `ReplaceContainerAsync(properties)`.
3. Upserts another sample order (same shape as Part 1's) and prints its `RequestCharge` --
   compare it to Part 1's write charge.
4. Re-runs the `orderLines` JOIN query from Part 1 and prints its `RequestCharge` --
   compare it to Part 1's.
5. Re-runs the two-property `ORDER BY` query -- this time it should succeed.

**Why the `ORDER BY` fails first, then succeeds:** Cosmos requires a composite index to sort
by two (or more) properties in a single query. Sorting by just one property never needs
this -- every property is range-indexed by default -- but the moment you ask for
`ORDER BY a, b`, Cosmos needs an index that's specifically ordered by `a` then `b` together,
which is exactly what a composite index is.

**Why the excluded-path query still works:** excluding `/orderLines/*` from the indexing
policy doesn't make it un-queryable -- Cosmos falls back to scanning within the partition
for that predicate instead of seeking through an index. Slower and pricier per query than an
indexed lookup would be, but the *write* savings across every order write is the actual
trade being made here, not query speed on a path this app doesn't query on its own hot path
anyway.

**Questions to think about:**
1. Why does the composite index need to specify both the properties *and* their sort
   directions (`customerId ASC, orderDate DESC`), rather than just the two property names?
2. You excluded `/orderLines/*` entirely rather than, say, `/orderLines/*/productId` only.
   What's the trade-off of excluding the whole subtree versus excluding just the one nested
   field you don't query on?

---

## Part 3: Reading `RequestCharge`

**Your Task:**
Create `Metrics/CosmosQueryExtensions.cs` with an extension method that runs a query to
completion and sums `RequestCharge` across every page (a query can return results across
several round trips, and each page reports its own charge):

```csharp
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Metrics;

public static class CosmosQueryExtensions
{
    public static async Task<(List<T> Items, double RequestCharge)> ExecuteAndMeasureAsync<T>(
        this Container container,
        QueryDefinition query,
        QueryRequestOptions? options = null)
    {
        var items = new List<T>();
        var totalCharge = 0.0;

        using var iterator = container.GetItemQueryIterator<T>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            totalCharge += page.RequestCharge;
            items.AddRange(page);
        }

        return (items, totalCharge);
    }
}
```

**Your Task:**
Create `Metrics/RequestChargeDemo.cs` that measures and prints `RequestCharge` for four
representative operations against the same order:
1. A **point read** -- `ReadItemAsync<Order>(id, partitionKey)`.
2. A **single-partition query** -- `WHERE o.customerId = @cid`, with `PartitionKey` set on
   the request options.
3. A **cross-partition query** -- `WHERE o.status = @status`, with no partition key set, so
   Cosmos fans out across every physical partition.
4. A **write** -- `UpsertItemAsync`.

**Why:** documentation can tell you point reads are cheap and cross-partition queries are
expensive, but only running it against your own data tells you *how much* cheaper, on your
own document shapes and your own dataset size. Expect the point read and single-partition
query to land close together and noticeably below the cross-partition query, even when the
cross-partition query returns a similar number of rows -- it's paying to visit every
partition, not just the one with the answer.

**Questions to think about:**
1. If you added a `GROUP BY o.status` aggregate query, would you expect its `RequestCharge`
   to be closer to the single-partition or the cross-partition number above? Why?
2. The point read needs both `id` and partition key, same as `ReadItemAsync` throughout this
   module. What would happen to its cost if you only had the `id` and had to find the
   partition key first with a query?

---

## Part 4: Manual vs. Autoscale Throughput

Cosmos containers (or databases, if you provision at that level instead) need RU/s
provisioned before they'll serve any requests. There are two ways to do it:
- **Manual**: a fixed RU/s value. Predictable cost, predictable capacity, and a hard
  ceiling -- traffic above it gets throttled (see Part 5).
- **Autoscale**: you set a *maximum* RU/s, and Cosmos automatically scales the actual
  provisioned throughput between 10% and 100% of that max based on real usage, billing for
  whatever it scaled to. Costs up to 1.5x the scaled-to RU/s compared to the equivalent
  manual value, in exchange for absorbing bursts without you provisioning for worst case
  up front.

**Your Task:**
Create `Throughput/ThroughputWalkthrough.cs` that provisions two new containers directly
from the client SDK:

```csharp
var manualProperties = new ContainerProperties("orders-manual", "/customerId");
var manualContainerResponse = await database.CreateContainerIfNotExistsAsync(
    manualProperties, ThroughputProperties.CreateManualThroughput(400));
var manualThroughput = await manualContainerResponse.Container.ReadThroughputAsync();
// manualThroughput == 400

var autoscaleProperties = new ContainerProperties("orders-autoscale", "/customerId");
var autoscaleContainerResponse = await database.CreateContainerIfNotExistsAsync(
    autoscaleProperties, ThroughputProperties.CreateAutoscaleThroughput(4000));
var throughputResponse = await autoscaleContainerResponse.Container.ReadThroughputAsync(new RequestOptions());
// throughputResponse.Resource.AutoscaleMaxThroughput == 4000, scales down to 400 (10%) when idle
```

Print both values.

**Why this is done from the client SDK, not `AppHost/Program.cs`:** the Aspire Azure Cosmos
DB hosting integration's `AddContainer` only accepts a name and a partition key path -- there
is no overload for a throughput profile or an indexing policy. Rather than fight the hosting
API for something it doesn't expose, both are configured here exactly the way you'd manage
them against a container in production that you don't want to tear down and recreate on
every deploy: through `ContainerProperties` / `ThroughputProperties` on the client SDK.

**Be honest about the emulator here:** it accepts both throughput APIs and reports the
values back correctly, but it does **not** meaningfully enforce RU/s limits or actually
scale capacity the way the real Cosmos DB service does. Treat this part as verifying you're
calling the provisioning APIs correctly -- not as a real load test of throttling behavior.
Part 5 covers what happens when a *real* Cosmos account does throttle you.

**Questions to think about:**
1. For a workload with a predictable, steady request rate, which throughput mode is
   probably cheaper, and why?
2. Autoscale's floor is 10% of its configured max. What does that imply about choosing a
   max that's much higher than you actually need "just in case"?

---

## Part 5: Handling `429 TooManyRequests`

When a request would exceed a container's provisioned RU/s, Cosmos DB returns
`429 TooManyRequests` instead of serving it, along with a `RetryAfter` telling you how long
to wait. The Cosmos SDK already retries these **for you** by default -- but there's a real
question of when that's enough and when it isn't.

**Your Task:**
Create `Retry/RetryPolicy.cs`:

```csharp
using System.Net;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Retry;

public static class RetryPolicy
{
    public static async Task<T> ExecuteWithManualRetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        Action<int, TimeSpan>? onThrottled = null)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "At least one attempt is required.");
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (CosmosException ex) when (ex.StatusCode == (HttpStatusCode)429 && attempt < maxAttempts)
            {
                var delay = ex.RetryAfter ?? TimeSpan.FromMilliseconds(500 * attempt);
                onThrottled?.Invoke(attempt, delay);
                await Task.Delay(delay);
            }
        }
    }
}
```

**Your Task:**
Write `Retry/RetryWalkthrough.cs` that wraps a write in `ExecuteWithManualRetryAsync`, and
separately constructs a `CosmosClientOptions` with
`MaxRetryAttemptsOnRateLimitedRequests` and `MaxRetryWaitTimeOnRateLimitedRequests` set,
printing both values. Note honestly that a single lightly-loaded client almost never gets
throttled by the emulator, so you likely won't see the manual retry branch actually fire
here -- that's expected, not a bug in your code. (The reference solution instead tests
`RetryPolicy`'s backoff logic directly against a constructed `CosmosException` in the test
project, rather than depending on real throttling that isn't reliably reproducible.)

**Why you'd write your own retry logic when the SDK already retries 429s:** the SDK's
built-in retry is silent -- it makes the throttling transparent to your code, which is
exactly what you want for an occasional, brief 429. What it doesn't give you is
**visibility**: if a container is *sustained*-throttled (under-provisioned for its real
load, not just a momentary spike), the SDK will happily keep retrying in the background
while your operation just gets slower and slower, with nothing in your logs pointing at RU
exhaustion as the cause. Manual retry logic like `RetryPolicy` above earns its place when
you want to *count* throttling events, log them, alert on a sustained streak, or fall back
to a cache -- not merely to survive a 429 that already gets retried for you.

**Questions to think about:**
1. If you set `MaxRetryAttemptsOnRateLimitedRequests = 0`, what would a 429 look like from
   your calling code's point of view?
2. Sketch (comments are fine) how you'd extend `RetryPolicy` to raise an alert after, say,
   5 consecutive throttled attempts across *different* calls, not just within one retry
   loop.

---

## Reflection Questions

After completing this exercise, answer these:

1. **Part 1 showed a cost with no offsetting benefit** (indexing `orderLines` when nothing
   queries into it). Part 2 fixed that but had to add a composite index to keep a query
   working. What general principle does that illustrate about indexing policy changes?
2. **RequestCharge is deterministic for a given operation and container state, but changes
   as your data and indexing policy change.** Why is "measure it yourself" better advice
   than memorizing a table of typical RU costs from documentation?
3. **Manual vs. autoscale throughput is a cost/predictability trade, not a
   correctness one.** Under what circumstances would picking the wrong one actually cause
   an outage, versus just costing more or less than necessary?
4. **The SDK already retries 429s by default.** Name one situation from a system you've
   used or built where "the failure is being silently handled" was itself the problem.
5. Which of this exercise's five parts would you expect to matter **least** for a
   container serving a low, steady trickle of traffic, and which would matter **most** for
   one serving a bursty, spiky one?

---

## Summary

You've learned:
- Cosmos DB's default indexing policy indexes everything automatically, including embedded
  arrays you may never query into
- How to exclude paths from indexing and add composite indexes, and why composite indexes
  are required for multi-property `ORDER BY`
- How to read and compare `RequestCharge` across point reads, single- and cross-partition
  queries, and writes
- The difference between manual and autoscale throughput, and that Cosmos hosting
  integrations for Aspire don't expose either from the AppHost -- both are client-SDK
  concerns
- How to catch and back off from `429 TooManyRequests` manually, and when that's worth
  doing on top of the SDK's own built-in retry

## Next Steps
- Compare your work against [`solution/`](solution/)
- Run the test suite: `dotnet test tests` (needs Docker -- see `GETTING_STARTED.md`)
- If you're working through module 11 in order, continue to
  **CosmosConsistencyAndTransactions**
