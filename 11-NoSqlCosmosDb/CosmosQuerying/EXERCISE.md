# Exercise: Querying Azure Cosmos DB

## Overview
In this exercise, you'll build a small retail-orders dataset in Azure Cosmos
DB and write the queries a real read-heavy application needs against it: the
LINQ provider, the SQL API with parameters, partition-aware queries,
efficient pagination, and projections that cut RU cost, not just payload
size. Everything runs against the **real Cosmos DB emulator** via .NET
Aspire -- there is no in-memory or "lite" substitute for a document database
whose whole story is partitioning and request-unit pricing.

## Learning Goals
By completing this exercise, you will:
- Query with `GetItemLinqQueryable<T>()` and know how it differs from EF
  Core's LINQ provider
- Write parameterized Cosmos SQL with `QueryDefinition` and `WithParameter`
  -- and know why that's non-negotiable, the same way it is for raw SQL
- Recognize a cross-partition query, scope a query to one partition with
  `QueryRequestOptions.PartitionKey`, and know when each is the right choice
- Page through results with `FeedIterator<T>` and continuation tokens, and
  know why that beats `Skip`/`Take` for anything but shallow paging
- Project a narrow field list to reduce **RU cost**, not just response size,
  and prove it by reading `RequestCharge`

---

## The Scenario

You're building the read side of a small e-commerce backend: `Customer` and
`Order` documents, seeded with 25 customers and 180 orders (1-4 line items
each) so filters, partition scoping, and pagination all have real data to
work against. Every query in this exercise is read-only.

---

## Part 0: Domain Model, Serialization, Containers, and Seed Data

Before you can query anything, you need two containers and some data.

### Step 0.1: Create the Domain Model

**Your Task:**
Create `Domain/Customer.cs`, `Domain/OrderLine.cs`, and `Domain/Order.cs`:

```csharp
// Domain/Customer.cs
namespace CosmosQuerying.Domain;

public class Customer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    /// <summary>"Standard", "Silver", or "Gold".</summary>
    public string Tier { get; set; } = "Standard";
}
```

```csharp
// Domain/OrderLine.cs
namespace CosmosQuerying.Domain;

public class OrderLine
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
```

```csharp
// Domain/Order.cs
namespace CosmosQuerying.Domain;

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }

    /// <summary>"Pending", "Shipped", "Delivered", or "Cancelled".</summary>
    public string Status { get; set; } = "Pending";

    public List<OrderLine> Lines { get; set; } = [];
    public decimal TotalAmount { get; set; }
}
```

**Why line items are embedded, not a separate container:** `OrderLine` only
ever gets read and written together with its parent `Order` -- nobody asks
for "all line items across every order" the way a relational report might.
Cosmos rewards embedding data with that access pattern; a separate
`orderLines` container would just mean an extra round trip for information
you always need anyway.

`Customer` lives in a container partitioned on `/id` -- one customer per
partition. That's a deliberate choice for small reference entities you
almost always look up by id and never query "every customer in partition
X": there's no better partition key than the id itself when there's no
other natural grouping key. `Order` is partitioned on `/customerId`, because
"this customer's orders" is by far the most common read pattern for an
orders container (Part 3 explores what that choice buys you, and costs you).

### Step 0.2: Fix the camelCase Trap Before It Bites You

**Your Task:**
Create `Persistence/CosmosSerialization.cs`:

```csharp
using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Persistence;

public static class CosmosSerialization
{
    public static CosmosClientOptions ClientOptions { get; } = new()
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
        },
    };

    public static CosmosLinqSerializerOptions LinqOptions { get; } = new()
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
    };
}
```

**Why this exists, and why it's two separate settings:** by default, the
Cosmos SDK serializes a C# property exactly as declared -- `CustomerId`
becomes the JSON field `"CustomerId"`, not `"customerId"`. Worse, your `Id`
property would be stored as `"Id"`, which Cosmos does **not** recognize as
its mandatory lowercase `id` system property, breaking every document's
identity. `ClientOptions.SerializerOptions` fixes how documents are written
and read. But `GetItemLinqQueryable<T>()` (Part 1) uses a **separate**
translator with its own naming setting -- get only one of the two right and
LINQ queries silently return zero rows, because the translator emits
`c["Status"]` while every document on disk actually has a lowercase
`status` field. Pass `CosmosSerialization.LinqOptions` to every
`GetItemLinqQueryable<T>()` call in this exercise, and
`CosmosSerialization.ClientOptions` to your one `CosmosClient`.

### Step 0.3: Ensure the Database and Containers Exist

**Your Task:**
Create `Persistence/CosmosInitializer.cs`:

```csharp
using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Persistence;

public static class CosmosInitializer
{
    public const string DatabaseName = "cosmosquerying";
    public const string CustomersContainer = "customers";
    public const string OrdersContainer = "orders";

    public static async Task<Database> EnsureDatabaseAsync(CosmosClient client, CancellationToken cancellationToken = default)
    {
        var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(DatabaseName, cancellationToken: cancellationToken);
        var database = databaseResponse.Database;

        await database.CreateContainerIfNotExistsAsync(CustomersContainer, "/id", cancellationToken: cancellationToken);
        await database.CreateContainerIfNotExistsAsync(OrdersContainer, "/customerId", cancellationToken: cancellationToken);

        return database;
    }
}
```

**Why `CreateIfNotExistsAsync` even when running under Aspire:** the AppHost
(see `GETTING_STARTED.md`) already declares these containers and provisions
them before this app starts. But this project should also work unmodified
against a manually-started emulator with nothing pre-provisioned -- exactly
like EfCoreQuerying's `EnsureCreatedAsync`. `CreateIfNotExistsAsync` makes
both paths safe: a no-op when Aspire already created things, the actual
provisioning step when it didn't.

### Step 0.4: Seed Deterministic Data

**Your Task:**
Create `Seed/OrderSeeder.cs` with a static `SeedAsync(Container customers, Container orders)` that:
1. Returns immediately if `customers` already has any documents (idempotent
   -- count with `SELECT VALUE COUNT(1) FROM c`).
2. Creates 25 customers (`cust-001` .. `cust-025`) with a random (but
   **seeded**, `new Random(20260915)`) name, city, and tier.
3. Creates 180 orders, each assigned to a random customer, with 1-4 line
   items, a random status, and an order date spread across roughly 20
   months.

The exact name pools, product list, and price ranges are up to you -- what
matters is **determinism**: run it twice against two empty databases and get
byte-identical data both times. See `../solution/Seed/OrderSeeder.cs` for one
complete implementation once you've tried your own.

**Why determinism matters here specifically:** every part after this one
queries this seed data and expects real matches -- orders with status
`"Shipped"`, a specific customer's order history, a result set large enough
to page through in more than one page. A non-deterministic seed would make
some runs pass and others fail for reasons that have nothing to do with your
query.

### Step 0.5: Wire It Up in Program.cs

**Your Task:**
Replace `Program.cs` with something that builds a `CosmosClient` from
`appsettings.json`'s `ConnectionStrings:cosmos` (using
`CosmosSerialization.ClientOptions`), ensures the database/containers exist,
and seeds:

```csharp
using CosmosQuerying.Persistence;
using CosmosQuerying.Seed;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connectionString = configuration.GetConnectionString("cosmos")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:cosmos.");

using var cosmosClient = new CosmosClient(connectionString, CosmosSerialization.ClientOptions);

var database = await CosmosInitializer.EnsureDatabaseAsync(cosmosClient);
var customers = database.GetContainer(CosmosInitializer.CustomersContainer);
var orders = database.GetContainer(CosmosInitializer.OrdersContainer);

await OrderSeeder.SeedAsync(customers, orders);

Console.WriteLine("Database ready and seeded.");
```

**Run it:**
```bash
# From this project's workspace folder, against a running emulator
# (see GETTING_STARTED.md):
dotnet run
```

---

## Part 1: The LINQ Provider

**Your Task:**
Query shipped orders over $100, newest first, projected to id/customerId/total:

```csharp
using Microsoft.Azure.Cosmos.Linq; // ToFeedIterator, ToQueryDefinition

var query = orders.GetItemLinqQueryable<Order>(linqSerializerOptions: CosmosSerialization.LinqOptions)
    .Where(o => o.Status == "Shipped" && o.TotalAmount > 100)
    .OrderByDescending(o => o.OrderDate)
    .Select(o => new { o.Id, o.CustomerId, o.TotalAmount });

// See the SQL this LINQ expression actually became:
Console.WriteLine(query.ToQueryDefinition().QueryText);

using var iterator = query.ToFeedIterator();
var results = new List<dynamic>();
double totalRu = 0;
while (iterator.HasMoreResults)
{
    var page = await iterator.ReadNextAsync();
    totalRu += page.RequestCharge;
    results.AddRange(page);
}
```

**How this differs from EF Core's LINQ provider:** `GetItemLinqQueryable<T>()`
looks identical to querying an EF Core `DbSet<T>` -- filter, sort, project,
nothing executes until you enumerate. But it translates to Cosmos's own SQL
dialect, not T-SQL, and it supports a **narrower** set of operators: no
joins across containers (Cosmos has no server-side join between containers
at all), and a smaller surface of translatable string/date methods than EF
Core's Npgsql or SQL Server providers expose. `ToQueryDefinition()` (from
`Microsoft.Azure.Cosmos.Linq`) is your escape hatch for "what SQL did this
actually become" whenever you're unsure.

**Questions to think about:**
1. What would you have to do differently to combine data from `customers`
   and `orders` in one query, given Cosmos has no cross-container join?
2. `Select` into an anonymous type or a DTO both work here -- which one
   would you reach for in a method whose return type other code depends on?

---

## Part 2: The SQL API with QueryDefinition

**Your Task:**
Query orders by status using a parameterized `QueryDefinition`:

```csharp
var status = "Cancelled";
var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
    .WithParameter("@status", status);

using var iterator = orders.GetItemQueryIterator<Order>(query);
var results = new List<Order>();
double totalRu = 0;
while (iterator.HasMoreResults)
{
    var page = await iterator.ReadNextAsync();
    totalRu += page.RequestCharge;
    results.AddRange(page);
}
```

**Why `WithParameter`, always:** this is the same lesson EfCoreQuerying
(module 10) teaches for `FromSqlInterpolated` vs. `FromSqlRaw` +
concatenation, applied to Cosmos's own SQL dialect. `WithParameter` sends
`@status` to the service as a real query parameter -- the value can never
restructure the query, no matter what it contains.

```csharp
// NEVER do this -- string-built Cosmos SQL is a SQL injection hole exactly
// like string-built T-SQL:
var query = new QueryDefinition($"SELECT * FROM c WHERE c.status = '{status}'");
// A status of  ' OR 1=1 --  turns the WHERE clause into "true for every row".
```

**Questions to think about:**
1. `WithParameter` accepts values of many CLR types (`string`, `decimal`,
   `bool`, ...) -- what do you think happens if you pass a value whose type
   doesn't match the field's actual JSON type in the stored documents?
2. Could a parameterized `QueryDefinition` still return more data than you
   intended if the query itself has no partition key filter? (Keep this in
   mind for Part 3.)

---

## Part 3: Cross-Partition vs. Partition-Scoped Queries

**Your Task:**
Run the same `WHERE c.customerId = @customerId` query two ways:

```csharp
var query = new QueryDefinition("SELECT * FROM c WHERE c.customerId = @customerId")
    .WithParameter("@customerId", customerId);

// No PartitionKey: Cosmos doesn't know from the query text alone which
// partition holds matches, so it fans the query out to every partition and
// merges the results.
using var crossPartitionIterator = orders.GetItemQueryIterator<Order>(query);

// The SAME query, scoped to one partition: Cosmos goes straight there.
var options = new QueryRequestOptions { PartitionKey = new PartitionKey(customerId) };
using var scopedIterator = orders.GetItemQueryIterator<Order>(query, requestOptions: options);
```

Run both to completion, summing `FeedResponse<T>.RequestCharge` across pages
for each, and compare.

**Why this matters:** every matching document happens to share one
`customerId`, but the **service** doesn't know that from the query text
alone -- only `QueryRequestOptions.PartitionKey` tells it. Without that hint,
Cosmos evaluates the query against every physical partition and merges the
results, which costs more RU and gets steadily worse as the container grows
more partitions. With the hint, Cosmos goes to exactly one partition.

**When to use which:** scope to a partition key whenever you know it up
front -- the overwhelmingly common case ("this customer's orders", "this
tenant's records"). Reach for a cross-partition query only when the query
genuinely spans partition-key values, e.g. an admin report over every order
regardless of customer.

**Questions to think about:**
1. If you queried `SELECT * FROM c` with no `WHERE` clause at all, is that
   query cross-partition or partition-scoped? Why?
2. What physical difference does `QueryRequestOptions.PartitionKey` actually
   make to how many partitions the service has to touch?

---

## Part 4: Pagination -- FeedIterator/Continuation Tokens vs. Skip/Take

**Your Task:**
Page through every order, 20 at a time, using `FeedIterator<T>`:

```csharp
var query = new QueryDefinition("SELECT * FROM c ORDER BY c.orderDate");
using var iterator = orders.GetItemQueryIterator<Order>(
    query, requestOptions: new QueryRequestOptions { MaxItemCount = 20 });

var pageCount = 0;
string? lastToken = null;
while (iterator.HasMoreResults)
{
    var page = await iterator.ReadNextAsync();
    pageCount++;
    lastToken = page.ContinuationToken; // persist this to resume later
}
```

Then compare against `Skip`/`Take` from LINQ:

```csharp
using var skipTakeIterator = orders
    .GetItemLinqQueryable<Order>(linqSerializerOptions: CosmosSerialization.LinqOptions)
    .OrderBy(o => o.Id)
    .Skip(100)
    .Take(20)
    .ToFeedIterator();
```

**Why continuation tokens win for deep paging:** a `FeedResponse<T>`'s
`ContinuationToken` is an opaque bookmark -- hand it back on a later call
(as the `continuationToken` argument to `GetItemQueryIterator`) and Cosmos
resumes exactly where it left off, without re-evaluating anything from
earlier pages. `Skip`/`Take` compiles to `OFFSET`/`LIMIT` in the generated
SQL, which **works**, but Cosmos still has to evaluate and discard every
skipped document server-side to reach your page -- asking for `Skip(100)`
costs more than `Skip(10)`, and costs the *same* 100-item scan again every
single time you ask for that page. A continuation token's cost doesn't grow
with how deep you've already paged.

**When Skip/Take is fine:** shallow, occasional paging (page 2, page 3 of a
small admin list). **When to prefer continuation tokens:** deep paging, or
any paging you expect to repeat often -- an infinite-scroll feed, an export
job walking millions of documents.

**Questions to think about:**
1. What would happen to the RU cost of `Skip(10000).Take(20)` compared to
   `Skip(20).Take(20)` against a container with 50,000 documents?
2. A continuation token is opaque and tied to the exact query that produced
   it -- what do you think happens if you change the query's `WHERE` clause
   but reuse an old continuation token from a different query?

---

## Part 5: Projections -- Reducing RU Cost, Not Just Payload Size

**Your Task:**
Compare a full-document read against a narrow projection for the same
logical query:

```csharp
public class OrderSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}
```

```csharp
var fullQuery = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
    .WithParameter("@status", "Delivered");
// ... read to completion, sum RequestCharge -> fullRu

var projectedQuery = new QueryDefinition(
        "SELECT c.id, c.customerId, c.totalAmount FROM c WHERE c.status = @status")
    .WithParameter("@status", "Delivered");
using var iterator = orders.GetItemQueryIterator<OrderSummaryDto>(projectedQuery);
// ... read to completion, sum RequestCharge -> projectedRu

Console.WriteLine($"Full: {fullRu:0.00} RU   Projected: {projectedRu:0.00} RU");
```

The same idea works from LINQ: `GetItemLinqQueryable<Order>().Select(o => new
{ o.Id, o.TotalAmount })` translates to the same kind of narrowed `SELECT`
list.

**Why this is about RU, not just bytes on the wire:** it's tempting to think
"the response is smaller, so it's faster" and stop there. The number that
actually matters for cost and throughput is `FeedResponse<T>.RequestCharge`
-- narrowing the `SELECT` list reduces how much of each document Cosmos has
to read and serialize server-side, which shows up as **fewer RU charged**
for the same logical query, not merely a smaller payload landing in your
process.

**Questions to think about:**
1. If a container's documents were tiny (just `id` and one number field),
   would you expect projection to save much RU? Why might the saving grow
   with document size?
2. `OrderSummaryDto` simply has no property for `Lines` or `Status` -- why
   does that matter more than it sounds like it should?

---

## Reflection Questions

After completing this exercise, answer these:

1. **Name one query EF Core's LINQ provider could express that Cosmos's
   LINQ provider cannot, and why** -- tie it back to how the two providers
   fundamentally differ in what they translate to.
2. **Why does `QueryDefinition` + `WithParameter` prevent SQL injection**
   when it looks, syntactically, almost identical to building the query
   string by hand?
3. **When would you deliberately choose a cross-partition query over a
   partition-scoped one**, given the RU cost difference this exercise
   showed?
4. **Why does a continuation token's cost not grow with how many pages
   you've already read, while `Skip`/`Take`'s cost does?**
5. **Projection saved RU in this exercise's tests -- under what
   circumstances might the saving be negligible?**

---

## Summary

You've learned:
- `GetItemLinqQueryable<T>()`, and how its translation target (Cosmos SQL,
  not T-SQL) makes it a narrower tool than EF Core's LINQ provider
- Parameterized Cosmos SQL with `QueryDefinition`/`WithParameter`, and why
  that's the only safe way to build a query from external input
- Cross-partition vs. partition-scoped queries via
  `QueryRequestOptions.PartitionKey`, and the RU cost difference between them
- `FeedIterator<T>` and continuation tokens for efficient deep pagination,
  vs. the RU cost profile of `Skip`/`Take`
- Projections that reduce RU cost by narrowing the `SELECT` list, verified
  against `RequestCharge` rather than assumed

## Next Steps

- Compare your work against [`solution/`](solution/)
- Run the test suite: `dotnet test tests` (needs Docker for the Cosmos DB
  emulator, orchestrated by Aspire -- see `GETTING_STARTED.md`)
- Explore the other projects in this module: data modeling
  (`CosmosModeling`), change feed, and the Cosmos-specific consistency and
  partitioning topics they cover
