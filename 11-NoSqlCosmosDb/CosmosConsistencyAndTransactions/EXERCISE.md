# Exercise: Consistency and Transactions in Azure Cosmos DB

## Overview
Module 10's `EfCoreTransactions` answered "what happens to concurrent
writers, and what happens when a multi-step operation fails halfway
through?" for a relational database with cross-table transactions,
row-level locking, and configurable isolation levels. This exercise asks
the exact same two questions of Cosmos DB, which answers them completely
differently: no cross-table (cross-container, cross-partition) transactions
at all, a *tunable* consistency model instead of fixed isolation levels,
and optimistic concurrency via HTTP ETags instead of a database-internal
version column.

You'll work through five parts: the five consistency levels and where
Cosmos's default sits among them, per-request overrides and session
tokens, ETag-based optimistic concurrency, `TransactionalBatch` for atomic
multi-item writes within one partition, and what to do when you need
atomicity across partitions (you can't have it -- so what do you do
instead?).

## Prerequisites
The Cosmos DB emulator must be running. Either:
```bash
dotnet run --project 11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/AppHost
```
which starts the emulator via Aspire and launches this project's
`solution/` wired to it, or start the emulator by hand (see the module
README) -- your workspace's `appsettings.json` already points at
`https://localhost:8081` with the emulator's well-known default key.

## A limitation to accept up front
The emulator is a **single node** with no cross-region replicas. Every
consistency level this exercise sets up will, physically, behave like
Strong/Session on the emulator -- there is no staleness for a weaker level
to visibly permit, because there's nothing to be stale *relative to*. What
you'll actually verify hands-on is everything that's real regardless of
topology: which levels exist, how to set them (at the client and per
request), the rule about weakening vs. strengthening, session tokens, and
the two mechanisms Cosmos gives you for concurrency and atomicity. This
exercise says so explicitly rather than pretending the emulator proves
something it can't.

## Learning Goals
By completing this exercise, you will:
- Know the five consistency levels, what each guarantees, and where each
  sits on the consistency/latency/availability spectrum
- Set a consistency level at the `CosmosClient` and override it per request
  -- and know the one hard rule that governs both: you can only ask for a
  level *weaker than or equal to* the account's configured default, never
  stronger
- Propagate a session token between `CosmosClient` instances to get
  read-your-own-writes under Session consistency across processes
- Use ETags for optimistic concurrency: read, note the ETag, write back
  with `IfMatchEtag`, and handle the `412 PreconditionFailed` that means
  someone beat you to it
- Use `TransactionalBatch` to make several writes in the same partition key
  succeed or fail together -- and explain exactly why it cannot do the same
  across two partition keys

---

## Part 0: The Domain and Wiring

### Step 0.1: The Documents

**Your Task:**
Create `Domain/Customer.cs`:

```csharp
using Newtonsoft.Json;

namespace CosmosConsistencyAndTransactions.Domain;

public class Customer
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("loyaltyPoints")]
    public int LoyaltyPoints { get; set; }

    [JsonProperty("tier")]
    public string Tier { get; set; } = "Standard";

    // Cosmos DB assigns and rewrites this on every write. Never set it
    // yourself -- it's the concurrency token used in Part 3.
    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}
```

**Why `[JsonProperty]` from Newtonsoft.Json, not `System.Text.Json`?** The
Cosmos SDK's default item serializer is built on Newtonsoft.Json
internally, regardless of what the rest of your app uses. That's also why
this project references the `Newtonsoft.Json` NuGet package explicitly --
without it, the build fails.

Create `Domain/OrderLine.cs`, `Domain/OrderStatus.cs`, and `Domain/Order.cs`:

```csharp
using Newtonsoft.Json;

namespace CosmosConsistencyAndTransactions.Domain;

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
```

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CosmosConsistencyAndTransactions.Domain;

[JsonConverter(typeof(StringEnumConverter))]
public enum OrderStatus
{
    Pending,
    Confirmed,
    Cancelled
}
```

```csharp
using Newtonsoft.Json;

namespace CosmosConsistencyAndTransactions.Domain;

public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("lines")]
    public List<OrderLine> Lines { get; set; } = [];

    [JsonProperty("status")]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [JsonProperty("totalAmount")]
    public decimal TotalAmount => Lines.Sum(l => l.Quantity * l.UnitPrice);

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}
```

### Step 0.2: A Deliberate Partition Key Choice -- Read This Before Part 4

**Your Task:**
Create `Domain/CustomerLoyaltyProfile.cs`:

```csharp
using Newtonsoft.Json;

namespace CosmosConsistencyAndTransactions.Domain;

public class CustomerLoyaltyProfile
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("loyaltyPoints")]
    public int LoyaltyPoints { get; set; }

    [JsonProperty("lifetimeOrderCount")]
    public int LifetimeOrderCount { get; set; }

    [JsonProperty("lifetimeSpend")]
    public decimal LifetimeSpend { get; set; }

    [JsonProperty("_etag")]
    public string? ETag { get; set; }

    public static string BuildId(string customerId) => $"profile-{customerId}";
}
```

**Why does this exist, when `Customer` already has a `LoyaltyPoints`
field?** This is the partition-key story, and it IS the lesson of Part 4.

The natural business operation this exercise wants to demonstrate is
"placing an order also credits loyalty points -- atomically, so a crash
between the two never leaves an order with no points, or points with no
order." The obvious data model puts `Customer` in a `customers` container
partitioned by `/id`, and `Order` in an `orders` container partitioned by
`/customerId` -- which is exactly what module 11's other projects do. But
`TransactionalBatch` (Part 4) only guarantees atomicity for operations
against documents that share **both** a container and a partition key
value. A `Customer` document (partition key `/id`, container `customers`)
and an `Order` document (partition key `/customerId`, container `orders`)
never share either one -- there is no way to batch them together, ever,
no matter what request options you pass.

This exercise's answer -- and a completely standard, real-world Cosmos
pattern -- is to keep the master `Customer` record where it is (used in
Parts 1-3), but maintain a second, denormalized `CustomerLoyaltyProfile`
document **inside the `orders` container, under the same `/customerId`
partition key as that customer's orders**. It duplicates a few fields, but
in exchange, a new order and its loyalty-point credit can now live in one
`TransactionalBatch`. This is the redesign-the-partition-key workaround
Part 5 discusses in the abstract, made concrete.

**Questions to think about:**
1. What's the cost of this duplication? What has to keep the master
   `Customer.LoyaltyPoints` and the `CustomerLoyaltyProfile.LoyaltyPoints`
   from drifting apart over time?
2. Could you avoid the duplication by moving the *entire* `Customer`
   document into the `orders` container instead? What would that cost
   every other query that only wants a customer by ID, cheaply?

### Step 0.3: Wire It Up

**Your Task:**
Create `CosmosOptions.cs` and `CosmosBootstrapper.cs`:

```csharp
namespace CosmosConsistencyAndTransactions;

public class CosmosOptions
{
    public string Database { get; set; } = "cosmostransactions";
    public string CustomersContainer { get; set; } = "customers";
    public string OrdersContainer { get; set; } = "orders";
    public string ConsistencyLevel { get; set; } = "Session";
}
```

```csharp
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions;

public static class CosmosBootstrapper
{
    public static async Task EnsureDatabaseAndContainersAsync(CosmosClient client, CosmosOptions options)
    {
        var database = (await client.CreateDatabaseIfNotExistsAsync(options.Database)).Database;
        await database.CreateContainerIfNotExistsAsync(options.CustomersContainer, "/id");
        await database.CreateContainerIfNotExistsAsync(options.OrdersContainer, "/customerId");
    }
}
```

Update `Program.cs`:

```csharp
using CosmosConsistencyAndTransactions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("cosmos")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:cosmos.");

var cosmosOptions = builder.Configuration.GetSection("Cosmos").Get<CosmosOptions>() ?? new CosmosOptions();

if (!Enum.TryParse<ConsistencyLevel>(cosmosOptions.ConsistencyLevel, ignoreCase: true, out var configuredLevel))
{
    configuredLevel = ConsistencyLevel.Session;
}

builder.Services.AddSingleton(cosmosOptions);
builder.Services.AddSingleton(_ => new CosmosClient(connectionString, new CosmosClientOptions
{
    ConsistencyLevel = configuredLevel
}));

var host = builder.Build();

var client = host.Services.GetRequiredService<CosmosClient>();
var options = host.Services.GetRequiredService<CosmosOptions>();

await CosmosBootstrapper.EnsureDatabaseAndContainersAsync(client, options);

var database = client.GetDatabase(options.Database);
var customers = database.GetContainer(options.CustomersContainer);
var orders = database.GetContainer(options.OrdersContainer);

Console.WriteLine("Schema ready. Now build Part 1 below.");
```

**Run it:**
```bash
dotnet run
```

---

## Part 1: The Five Consistency Levels

### The Spectrum
Cosmos DB offers five consistency levels, strongest (most guarantees,
highest latency/cost, lowest availability during a regional outage) to
weakest (fewest guarantees, lowest latency/cost, highest availability):

| Level | Guarantee |
|---|---|
| **Strong** | Every read sees the latest committed write. No staleness, ever. |
| **Bounded Staleness** | Reads lag writes by at most *K* versions or *T* time (both configurable). |
| **Session** (Cosmos's default) | Within one session -- tracked via a session token, see Part 2 -- you always read your own writes. |
| **Consistent Prefix** | Reads never see writes out of order, but may be arbitrarily stale. |
| **Eventual** | Reads may be out of order and arbitrarily stale. Weakest, cheapest, most available. |

**The one rule that governs everything else in this exercise:** a client
or a single request can only ask for a level *weaker than or equal to*
the database account's configured default. You can always relax toward
Eventual; you can never strengthen past whatever the account promises.
Asking for something stronger doesn't upgrade your guarantee -- the SDK
either clamps it or rejects it.

### Step 1.1: Read the Account Default, Set the Client Default

**Your Task:**
Create `Demos/ConsistencyLevelDemo.cs`:

```csharp
using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Demos;

public static class ConsistencyLevelDemo
{
    private static readonly (ConsistencyLevel Level, string Description)[] LevelsStrongestFirst =
    [
        (ConsistencyLevel.Strong, "Every read sees the latest committed write."),
        (ConsistencyLevel.BoundedStaleness, "Reads lag writes by at most K versions or T time."),
        (ConsistencyLevel.Session, "DEFAULT. Read-your-own-writes within one session."),
        (ConsistencyLevel.ConsistentPrefix, "Never out of order, but may be arbitrarily stale."),
        (ConsistencyLevel.Eventual, "May be out of order and arbitrarily stale.")
    ];

    public static async Task RunAsync(CosmosClient client, string connectionString, Container customers)
    {
        var account = await client.ReadAccountAsync();
        var accountDefault = account.Consistency.DefaultConsistencyLevel;

        Console.WriteLine($"Account-level default consistency: {accountDefault}");
        Console.WriteLine($"This CosmosClient was constructed with: {client.ClientOptions.ConsistencyLevel}");

        foreach (var (level, description) in LevelsStrongestFirst)
        {
            Console.WriteLine($"  {level,-17} {description}");
        }
    }
}
```

`CosmosClientOptions.ConsistencyLevel` is how you set the client-wide
default -- pass it in the constructor, as `Program.cs` already does.
`client.ReadAccountAsync().Consistency.DefaultConsistencyLevel` tells you
what the *account* actually allows.

### Step 1.2: Prove the Weaken-Only Rule

**Your Task:**
Extend `ConsistencyLevelDemo.RunAsync` to construct a fresh `CosmosClient`
per level and skip any level stronger than the account default:

```csharp
    foreach (var (level, _) in LevelsStrongestFirst)
    {
        if (Rank(level) > Rank(accountDefault))
        {
            Console.WriteLine($"  {level,-17} SKIPPED -- stronger than the account default; not allowed.");
            continue;
        }

        using var probeClient = new CosmosClient(connectionString, new CosmosClientOptions { ConsistencyLevel = level });
        var container = probeClient.GetContainer(customers.Database.Id, customers.Id);

        var probe = new Customer { Name = $"ConsistencyProbe-{level}", Email = "probe@example.com" };
        var response = await container.CreateItemAsync(probe, new PartitionKey(probe.Id));

        Console.WriteLine($"  {level,-17} client constructed and wrote OK (RU charge {response.RequestCharge:0.00}).");
    }
}

private static int Rank(ConsistencyLevel level) => level switch
{
    ConsistencyLevel.Strong => 4,
    ConsistencyLevel.BoundedStaleness => 3,
    ConsistencyLevel.Session => 2,
    ConsistencyLevel.ConsistentPrefix => 1,
    ConsistencyLevel.Eventual => 0,
    _ => -1
};
```

**Run it:**
```bash
dotnet run -- 1
```

**Questions to think about:**
1. If the emulator's account default were `Eventual` instead of `Session`,
   which levels in the loop above would get skipped?
2. Why does asking for a *stronger* level than the account default not
   just "try harder" and sometimes succeed? What would that imply about
   the account's other regions?

---

## Part 2: Per-Request Overrides and Session Tokens

### Overriding Consistency for One Request
The same weaken-only rule from Part 1 applies at the level of a single
request, not just a client: `ItemRequestOptions.ConsistencyLevel` (for
point reads) and `QueryRequestOptions.ConsistencyLevel` (for queries) both
exist in this SDK version and let one call ask for something weaker than
the client's configured default -- useful when most of your app needs
Session consistency, but one specific read (an analytics dashboard, say)
would rather trade correctness for speed.

### Session Tokens: What "Read Your Own Writes" Actually Requires
Session consistency's guarantee is scoped to *a session* -- which, in
practice, means whichever requests carry the same session token. A single
`CosmosClient` instance tracks and attaches this automatically across its
own calls. The moment your app has **more than one `CosmosClient`
instance** (multiple pods behind a load balancer, a separate background
worker, etc.), that automatic tracking stops helping: a read from
`clientB` was never part of `clientA`'s session, so Session consistency
gives it no guarantee about `clientA`'s writes -- unless you manually
carry the token over via `ItemResponse<T>.Headers.Session` (from the
write) into `ItemRequestOptions.SessionToken` (on the read).

### Step 2.1: Override a Read, Then Propagate a Token Across Clients

**Your Task:**
Create `Demos/SessionTokenDemo.cs`:

```csharp
using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Demos;

public static class SessionTokenDemo
{
    public static async Task RunAsync(string connectionString, Container customers)
    {
        var customer = new Customer { Name = "Session Demo", Email = "session@example.com", LoyaltyPoints = 10 };
        var writeResponse = await customers.CreateItemAsync(customer, new PartitionKey(customer.Id));
        Console.WriteLine($"Created customer {customer.Id}. Write response session token: {writeResponse.Headers.Session}");

        // Per-request override -- must be weaker than or equal to the client default.
        var weakReadOptions = new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Eventual };
        var weakRead = await customers.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id), weakReadOptions);
        Console.WriteLine($"Read back with a per-request Eventual override: LoyaltyPoints={weakRead.Resource.LoyaltyPoints}");

        // A SECOND CosmosClient -- e.g. another pod -- that never saw the write above.
        using var otherClient = new CosmosClient(connectionString, new CosmosClientOptions { ConsistencyLevel = ConsistencyLevel.Session });
        var otherContainer = otherClient.GetContainer(customers.Database.Id, customers.Id);

        var withoutToken = await otherContainer.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));
        Console.WriteLine($"Second client, no session token propagated: LoyaltyPoints={withoutToken.Resource.LoyaltyPoints}");

        var withTokenOptions = new ItemRequestOptions { SessionToken = writeResponse.Headers.Session };
        var withToken = await otherContainer.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id), withTokenOptions);
        Console.WriteLine($"Second client, WITH propagated session token: LoyaltyPoints={withToken.Resource.LoyaltyPoints}");
    }
}
```

**Run it:**
```bash
dotnet run -- 2
```

**On this emulator, both reads above return the same value** -- there's
only one node, so there's nothing for a missing token to expose. The code
is still correct and is exactly what you'd need in production; the
emulator just can't fail the "without token" case for you to see.

**Questions to think about:**
1. If a caller loses a session token (e.g. an app restart with no
   persisted token), what's the practical fallback? What does it cost?
2. Why is it `ItemResponse<T>.Headers.Session` on the *write* response, but
   `ItemRequestOptions.SessionToken` on the *next read* -- why isn't there
   a single ambient token the SDK tracks for you across two different
   `CosmosClient` objects?

---

## Part 3: ETag-Based Optimistic Concurrency

### The Problem, Again
Same lost-update scenario as module 10's `EfCoreTransactions` Part 4: two
requests read the same document, both compute a new value from what they
read, and without a concurrency check, the second write silently
overwrites the first.

### Cosmos's Answer: ETags and `IfMatchEtag`
Every Cosmos document carries a server-assigned `_etag` that changes on
every write. `ItemRequestOptions.IfMatchEtag` (inherited from the base
`RequestOptions`) makes a write conditional: it only applies if the
document's current `_etag` still matches the one you pass. If it doesn't
-- because someone else wrote first -- Cosmos returns HTTP `412
Precondition Failed`, surfaced by the SDK as a `CosmosException` with
`StatusCode == HttpStatusCode.PreconditionFailed`.

### Step 3.1: Force a 412, Then Reload-and-Retry

**Your Task:**
Create `Demos/OptimisticConcurrencyDemo.cs`:

```csharp
using System.Net;
using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Demos;

public static class OptimisticConcurrencyDemo
{
    public static async Task RunAsync(Container customers)
    {
        var customer = new Customer { Name = "Concurrency Demo", Email = "concurrency@example.com", LoyaltyPoints = 100 };
        await customers.CreateItemAsync(customer, new PartitionKey(customer.Id));

        // Two "requests" both read the SAME version.
        var readA = await customers.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));
        var readB = await customers.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));

        var customerA = readA.Resource;
        customerA.LoyaltyPoints += 50;
        var writeA = await customers.ReplaceItemAsync(
            customerA, customerA.Id, new PartitionKey(customerA.Id),
            new ItemRequestOptions { IfMatchEtag = readA.ETag });
        Console.WriteLine($"Writer A succeeded. points={writeA.Resource.LoyaltyPoints}.");

        var customerB = readB.Resource;
        customerB.LoyaltyPoints += 20;
        try
        {
            await customers.ReplaceItemAsync(
                customerB, customerB.Id, new PartitionKey(customerB.Id),
                new ItemRequestOptions { IfMatchEtag = readB.ETag }); // readB's ETag is now STALE
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            Console.WriteLine("Writer B: 412 PreconditionFailed -- someone else changed this document first.");

            var reloaded = await customers.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));
            reloaded.Resource.LoyaltyPoints += 20; // reapply B's intent on the CURRENT value
            var retry = await customers.ReplaceItemAsync(
                reloaded.Resource, reloaded.Resource.Id, new PartitionKey(reloaded.Resource.Id),
                new ItemRequestOptions { IfMatchEtag = reloaded.ETag });
            Console.WriteLine($"Retry succeeded. Final points={retry.Resource.LoyaltyPoints} (100 + 50 + 20).");
        }
    }
}
```

**Run it:**
```bash
dotnet run -- 3
```

**Compare to module 10.** `EfCoreTransactions` Part 4 does the *identical*
compare-and-swap: read a version marker (there, Postgres's server-assigned
`xmin`), write back only if the marker hasn't moved, catch the failure
(`DbUpdateConcurrencyException`, backed by a zero-rows-affected `UPDATE ...
WHERE xmin = @original`), reload, retry. Here, the marker is an HTTP ETag,
the check is an `If-Match` precondition header, and the failure is a `412`
status code instead of a rows-affected count. Same idea; the database's
transport (SQL rows vs. HTTP documents) changes the mechanism, not the
concept.

**Questions to think about:**
1. `reloaded.Resource.LoyaltyPoints += 20` re-applies B's *intent* on top
   of whatever the document looks like now, rather than re-sending B's
   original stale object. Why does the retry have to work that way, same
   as module 10's `ReloadAsync()` + reapply pattern?
2. What would happen if you retried by resending `customerB` (the original
   stale object) with the NEW ETag instead of reloading first? What data
   would you silently lose?

---

## Part 4: `TransactionalBatch` -- Atomicity Within One Partition

### What It Does
`container.CreateTransactionalBatch(partitionKey)` lets you queue several
operations -- `CreateItem`, `ReplaceItem`, `UpsertItem`, `ReadItem`,
`DeleteItem`, `PatchItem` -- against documents that **all share the same
partition key value in the same container**, and `ExecuteAsync()` sends
them as one atomic unit: every operation succeeds, or none of them take
effect. Check `TransactionalBatchResponse.IsSuccessStatusCode` to know
which happened; per-operation detail is available via
`GetOperationResultAtIndex<T>(index)`.

### The Scenario
Placing an order should credit loyalty points, atomically -- so a failure
partway through never leaves an order with no points credited, or points
credited with no order to show for them. Per Part 0's `CustomerLoyaltyProfile`,
that document lives in the same `orders` container and the same
`/customerId` partition as the new `Order`, so both fit in one batch.

### Step 4.1: A Successful Batch

**Your Task:**
Create `Demos/TransactionalBatchDemo.cs`:

```csharp
using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Demos;

public static class TransactionalBatchDemo
{
    public static async Task RunAsync(Container orders)
    {
        var customerId = Guid.NewGuid().ToString();
        var profile = new CustomerLoyaltyProfile { Id = CustomerLoyaltyProfile.BuildId(customerId), CustomerId = customerId };
        var profileCreate = await orders.CreateItemAsync(profile, new PartitionKey(customerId));

        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Confirmed,
            Lines = [new OrderLine { ProductId = "sku-1", ProductName = "Widget", Quantity = 3, UnitPrice = 25m }]
        };
        var earnedPoints = (int)order.TotalAmount;

        var updatedProfile = profileCreate.Resource;
        updatedProfile.LoyaltyPoints += earnedPoints;

        var batch = orders.CreateTransactionalBatch(new PartitionKey(customerId))
            .CreateItem(order)
            .ReplaceItem(updatedProfile.Id, updatedProfile, new TransactionalBatchItemRequestOptions { IfMatchEtag = profileCreate.ETag });

        using var batchResponse = await batch.ExecuteAsync();
        Console.WriteLine($"Batch succeeded: {batchResponse.IsSuccessStatusCode}");
    }
}
```

Notice `TransactionalBatchItemRequestOptions.IfMatchEtag` on the
`ReplaceItem` call -- ETag concurrency and `TransactionalBatch` compose:
each operation in a batch can carry its own precondition.

### Step 4.2: A Failure That Rolls Back BOTH Operations

**Your Task:**
Extend `RunAsync` to engineer a stale ETag on the profile operation and
confirm neither write landed:

```csharp
        var staleProfileEtag = profileCreate.ETag; // deliberately OLD
        var secondOrder = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Confirmed,
            Lines = [new OrderLine { ProductId = "sku-1", ProductName = "Widget", Quantity = 1, UnitPrice = 999m }]
        };

        var currentProfile = await orders.ReadItemAsync<CustomerLoyaltyProfile>(updatedProfile.Id, new PartitionKey(customerId));
        currentProfile.Resource.LoyaltyPoints += (int)secondOrder.TotalAmount;

        var failingBatch = orders.CreateTransactionalBatch(new PartitionKey(customerId))
            .CreateItem(secondOrder)
            .ReplaceItem(currentProfile.Resource.Id, currentProfile.Resource,
                new TransactionalBatchItemRequestOptions { IfMatchEtag = staleProfileEtag });

        using var failingResponse = await failingBatch.ExecuteAsync();
        Console.WriteLine($"Deliberately-conflicting batch succeeded: {failingResponse.IsSuccessStatusCode}"); // false

        // Prove the order was NEVER created -- not "created then rolled back",
        // genuinely never persisted.
        try
        {
            await orders.ReadItemAsync<Order>(secondOrder.Id, new PartitionKey(customerId));
            Console.WriteLine("BUG: the order exists despite the batch failing.");
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Console.WriteLine("Confirmed: the order from the failed batch does not exist. True atomicity.");
        }
```

**Run it:**
```bash
dotnet run -- 4
```

**Questions to think about:**
1. `TransactionalBatch` never throws for a failed precondition inside the
   batch -- it returns a response with `IsSuccessStatusCode == false`.
   Contrast that with Part 3's single-item `ReplaceItemAsync`, which DOES
   throw a `CosmosException`. Why might a batch API prefer "tell me in the
   response" over "throw"?
2. Module 10's `EfCoreTransactions` Part 2 wraps a debit and a credit in
   `BeginTransactionAsync()`/`CommitAsync()`/`RollbackAsync()` -- three
   separate calls around arbitrary work. `TransactionalBatch` is a single
   `ExecuteAsync()` call with a fixed set of operations decided up front.
   What could you do inside an EF Core transaction that you fundamentally
   cannot do inside a `TransactionalBatch`?

---

## Part 5: When This Breaks -- No Cross-Partition Transactions

### The Wall
`TransactionalBatch` cannot help you if the two documents that must move
together live in **different partition keys** -- even in the same
container. There's no request option, no client flag, no server
configuration that changes this. It's a structural property of how Cosmos
DB distributes data: a partition key's data lives together on one
physical partition specifically so operations within it can be atomic and
fast; anything crossing partitions is, by definition, crossing physical
boundaries with no shared transaction log to coordinate them.

### Step 5.1: See It Rejected

**Your Task:**
Create `Demos/CrossPartitionLimitationsDemo.cs`:

```csharp
using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Demos;

public static class CrossPartitionLimitationsDemo
{
    public static async Task RunAsync(Container orders)
    {
        var customerA = Guid.NewGuid().ToString();
        var customerB = Guid.NewGuid().ToString();
        var orderForB = new Order { CustomerId = customerB, Status = OrderStatus.Confirmed };

        try
        {
            var batch = orders.CreateTransactionalBatch(new PartitionKey(customerA)) // customerA's partition...
                .CreateItem(orderForB);                                              // ...but this item belongs to customerB

            using var response = await batch.ExecuteAsync();
            Console.WriteLine($"Batch status: {response.StatusCode} (IsSuccessStatusCode={response.IsSuccessStatusCode})");
        }
        catch (Exception ex) when (ex is CosmosException or ArgumentException)
        {
            Console.WriteLine($"Rejected before it could do anything: {ex.GetType().Name}");
        }
    }
}
```

**Run it:**
```bash
dotnet run -- 5
```

### Real-World Workarounds
When you genuinely need two different partitions' documents to change
together, you have three real options -- none of them "just do it anyway":

1. **Redesign the partition key.** This is what Part 4's
   `CustomerLoyaltyProfile` did: it copied the data that needed atomicity
   into the same partition as the thing it needed to be atomic with. If
   two entities need to move together often enough, that's a signal they
   may belong in the same partition.
2. **Sagas / compensating actions.** Apply each partition's change as its
   own step, record progress somewhere durable (a "saga" or "process"
   document), and if a later step fails, run an explicit compensating
   write to undo the earlier ones. You own the rollback logic end-to-end;
   Cosmos gives you nothing here for free. This exercise doesn't build a
   full saga -- just know the shape: forward steps + recorded progress +
   compensations, not a single all-or-nothing call.
3. **Accept eventual consistency between the partitions and reconcile
   asynchronously** -- e.g. a change feed processor that notices one side
   changed and updates the other. See `CosmosChangeFeed` elsewhere in this
   module for exactly that pattern.

**Questions to think about:**
1. Which of the three workarounds would you reach for if the two
   partitions belonged to two different *customers* placing orders that
   must be fulfilled together (e.g. a gift exchange)? Which would you
   reach for if they belonged to the same customer's `Order` and
   `Customer` documents (the original motivating case from Part 0)?
2. A saga's compensating action is application code you write and test
   yourself -- it can have its own bugs. What does that imply about how
   much you should prefer option 1 (redesign the partition key) whenever
   it's actually available?

---

## Checking Your Work
A complete reference implementation lives in `solution/`, organized as one
file per part under `solution/Demos/`, and `tests/` proves the behavior
against a real, disposable Cosmos DB emulator instance (via
`Aspire.Hosting.Testing`).

```bash
dotnet run --project solution -- 1     # or 2, 3, 4, 5, or "all"
dotnet test tests                      # needs Docker running (starts the emulator itself)
```

Build your own version of each part first. When you compare, notice in
particular how `solution/Domain/CustomerLoyaltyProfile.cs`'s XML doc
comment spells out the partition-key reasoning from Part 0 in more detail
than this file does.

---

## Reflection Questions

1. **Session consistency is the default for a reason -- it's "free"
   read-your-own-writes for the overwhelmingly common case of a single
   client talking to itself.** What does an application have to get right
   for that default to actually hold once it's more than one process?

2. **`IfMatchEtag` and `TransactionalBatch`'s per-operation
   `IfMatchEtag` are the same mechanism used two different ways -- single-
   document compare-and-swap vs. multi-document all-or-nothing.** What do
   they have in common, and what does `TransactionalBatch` add on top?

3. **Compare Cosmos's consistency levels to Postgres's isolation levels
   from module 10.** Isolation levels govern what one transaction can see
   of OTHER concurrent transactions' uncommitted or recently-committed
   work; consistency levels govern how fresh a READ REPLICA's data is
   relative to the primary. Are these actually answering the same
   question, or two different ones that are easy to conflate?

4. **Why does `TransactionalBatch` require every operation's partition key
   to match, when a single container can hold documents from many
   different partition keys?** What would Cosmos have to give up,
   architecturally, to relax that requirement?

5. **You now have two "detect a conflicting concurrent write" mechanisms
   in your toolkit: `xmin` (module 10) and `_etag` (this module).** If you
   were designing a brand-new system from scratch with a free choice of
   database, what would make you pick one storage model over the other
   specifically because of how each handles this problem?

---

## Summary

You've learned:
- The five consistency levels, Cosmos's Session default, and the
  weaken-only rule for client- and request-level overrides
- Session tokens, and why propagating them matters the moment an app has
  more than one `CosmosClient`
- ETag-based optimistic concurrency: `IfMatchEtag`, catching `412
  PreconditionFailed`, and reload-and-retry -- module 10's `xmin` pattern,
  translated to HTTP
- `TransactionalBatch`: atomic multi-item writes scoped to one partition
  key, and a concrete example of redesigning a partition key specifically
  to make an atomic operation possible
- Why Cosmos has no cross-partition transaction, and the three real
  workarounds when you need one anyway

## Next Steps
This is currently the last project in this module. Go back and compare
this project against
[10-EntityFrameworkCore/EfCoreTransactions](../../10-EntityFrameworkCore/EfCoreTransactions/)
side by side -- same two questions (concurrent writers, atomic multi-step
operations), two databases with almost nothing in common in how they
answer them.

---

**Happy Learning!**
