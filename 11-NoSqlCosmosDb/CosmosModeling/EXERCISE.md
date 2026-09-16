# Exercise: Modeling Documents and Partition Keys in Cosmos DB

## Overview
Module 10 gave you a normalized relational schema: `Author`, `Publisher`, `Book`,
`Review`, tied together with foreign keys and joined back together with `Include`. This
exercise is the direct NoSQL counterpart, using the same kind of domain -- retail orders
instead of a library -- to make the contrast concrete: no foreign keys, no joins, no
schema migrations. Just documents, and a handful of deliberate choices about what goes in
one document versus another, and how those documents get spread across physical storage.

Everything here runs against the **real Cosmos DB emulator** via .NET Aspire. There's no
in-memory substitute that would teach you anything about partition keys or RU cost, so
this exercise (like module 10's) only really works against the real thing.

## Learning Goals
By completing this exercise, you will:
- Model a `Customer` document and choose a partition key for it, and explain why point
  lookups make `/id` the right choice
- Model an `Order` document with **embedded** order lines, choose `/customerId` as its
  partition key, and explain the trade-off of a single customer's orders sharing one
  partition forever
- Decide when to embed related data in a document versus referencing it from a separate
  container, using order lines (embed) and a hypothetical product catalog (reference) as
  the two contrasting cases
- Evolve a document's shape over time with no migration step, and write code that
  tolerates both the old and new shapes
- Build a synthetic, composite partition key to bound the growth of a single very active
  customer's partition

---

## The Scenario

You're building the data layer for a retail order system: customers, and the orders they
place. Two containers:
- `customers` -- one document per customer
- `orders` -- one document per order, with that order's line items embedded directly in it

Both containers, and the emulator that hosts them, are already wired up for you in
`AppHost/Program.cs` -- this exercise is about the documents and their partition keys, not
about standing up Cosmos DB itself.

---

## Part 0: Project Setup

**Your Task:**
In the `CosmosModeling/` workspace project (the one with `Program.cs` that currently just
prints a placeholder message), create the following empty folders to hold your work as you
go: `Domain/`, `Persistence/`, `Demos/`.

Your `appsettings.json` already points at `https://localhost:8081` with the Cosmos
emulator's well-known default key -- you don't need to change it. When you run via
`AppHost`, Aspire overrides this connection string automatically with the one for the
container it started; when you run standalone (see `GETTING_STARTED.md`), this file's
value is what gets used.

**Why:** unlike module 10's PostgreSQL, where the schema lives in the database, a Cosmos
container doesn't know or care what shape its documents are ahead of time -- the "schema"
is entirely a property of your C# classes and the code that reads/writes them. Everything
you build in `Domain/` in this exercise is doing work that, in module 10, the database
schema and EF Core migrations did for you.

---

## Part 1: The Customer Document and Its Partition Key

**Your Task:**
Create `Domain/Customer.cs`:

```csharp
using Newtonsoft.Json;

namespace CosmosModeling.Domain;

public class Customer
{
    // Cosmos requires every item to have an "id" property (lowercase, exactly that name),
    // unique within its partition. JsonProperty maps this PascalCase C# property onto that
    // required lowercase JSON field.
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

**Why `/id` as the partition key:** customers are almost always looked up one at a time,
by their own id -- a profile page, an order confirmation, a support ticket. There's no
"list all customers matching some criteria" query in this domain that matters the way
"list this customer's orders" does for `Order`. Partitioning by the item's own id gives
Cosmos the best possible distribution (every customer is, by definition, a different
partition key value, so no two customers can ever collide into a hot partition) and turns
every lookup into the cheapest operation Cosmos offers: a single-partition point read.

**Your Task:**
Create `Persistence/CosmosInitializer.cs` with a static class exposing:
- `EnsureDatabaseAsync(CosmosClient client)` — `client.CreateDatabaseIfNotExistsAsync("cosmosmodeling")`
- `EnsureCustomersContainerAsync(Database database)` — `database.CreateContainerIfNotExistsAsync("customers", "/id")`

Both calls are idempotent no-ops when Aspire has already provisioned them (the normal way
you'll run this project), and are what makes running directly against a manually-started
emulator work with no other changes.

**Your Task:**
In `Program.cs`, build a `CosmosClient` from `appsettings.json`'s `ConnectionStrings:cosmos`,
call your two `EnsureXAsync` methods, then write a demo (`Demos/Part1CustomerDocument.cs`
is a reasonable name) that:
1. Creates a `Customer` and upserts it with `UpsertItemAsync(customer, new PartitionKey(customer.Id))`.
2. Reads it back with `ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id))`
   and prints `response.RequestCharge` — the RU cost of that point read.

**Questions to think about:**
1. What would happen to write distribution if you partitioned `Customer` by, say, a fixed
   `"customers"` string on every document instead of `/id`? (Every customer would land in
   the same logical partition — think about why that's the worst possible choice here.)
2. `ReadItemAsync` needs both the id and the partition key. Why can't Cosmos find an item
   by id alone without you also supplying the partition key it lives in?

---

## Part 2: The Order Document With Embedded Order Lines

**Your Task:**
Create `Domain/OrderLine.cs`:

```csharp
namespace CosmosModeling.Domain;

public class OrderLine
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
```

Then `Domain/Order.cs`:

```csharp
using Newtonsoft.Json;

namespace CosmosModeling.Domain;

public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Must serialize to exactly "customerId" (lowercase c) to match the container's
    // partition key path -- Cosmos matches partition key paths against the JSON on the
    // wire, not against your C# property name.
    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Pending";
    public List<OrderLine> OrderLines { get; set; } = [];
}
```

**Why `/customerId` as the partition key:** the dominant query against orders in this
domain is "give me this customer's orders" — an order history page, a support lookup, a
re-order flow. Partitioning by `CustomerId` keeps every order a customer has ever placed
co-located in one logical partition, so that query is a cheap **single-partition** query
instead of a cross-partition fan-out across the whole container.

**The trade-off, explicitly:** every order a customer ever places lands in that same
logical partition, forever. Cosmos caps a single logical partition at 20GB of storage and
gives it a share of the container's overall provisioned throughput. For the overwhelming
majority of customers this is a non-issue. For one extremely high-volume customer — think
a business account placing thousands of orders a month, not a person shopping for
groceries — that partition keeps growing without bound, and eventually becomes both a
storage and a throughput hot spot. Part 5 comes back to exactly this case.

**Your Task:**
Add `EnsureOrdersContainerAsync(Database database)` to `CosmosInitializer`
(`database.CreateContainerIfNotExistsAsync("orders", "/customerId")`), then write
`Demos/Part2OrderDocument.cs`:
1. Create an `Order` for a new customer with two `OrderLine`s, and upsert it under
   `new PartitionKey(order.CustomerId)`.
2. Query for that customer's orders using `GetItemLinqQueryable<Order>` with
   `QueryRequestOptions { PartitionKey = new PartitionKey(customerId) }` set, and print how
   many came back and the query's `RequestCharge`.

```csharp
var query = orders
    .GetItemLinqQueryable<Order>(requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(customerId) })
    .Where(o => o.CustomerId == customerId);

using var iterator = query.ToFeedIterator();
var page = await iterator.ReadNextAsync();
```

**Why setting `PartitionKey` on the request options matters:** without it, this is a
**cross-partition** query — Cosmos has to check every physical partition in the container
for matches, which costs more RUs and more latency as the container grows, even though the
`Where` clause would still return the correct rows. Supplying the partition key tells
Cosmos exactly which partition to look in, turning an O(partitions) operation into O(1).

**Questions to think about:**
1. If you ran the same `Where(o => o.CustomerId == customerId)` query *without* setting
   `PartitionKey` on the request options, would you get different *results*? Would you get
   a different *cost*?
2. What real-world order query would force you into a cross-partition query no matter how
   you'd chosen this partition key — e.g. "all orders placed today, across every
   customer"? Why does no single-property partition key make that query cheap?

---

## Part 3: Embedding vs. Referencing

**Your Task:**
Write `Demos/Part3EmbeddingVsReferencing.cs` that reads back an `Order` with
`ReadItemAsync` and prints its `OrderLines.Count` alongside the single `RequestCharge` that
read cost — proving the order and every one of its line items came back in one round trip.

**Why `OrderLine[]` is embedded, not a separate `OrderLines` container referenced by
`OrderId`:** order lines are always read and written together with their order, and are
never queried independently — "show me line item X in isolation" isn't a real use case
here. A separate `OrderLines` container would mean every screen that shows an order (which
is every screen that touches orders at all) needs a **second round trip** to fetch its
lines — and, unless that container were also carefully partitioned by `customerId`, quite
possibly a cross-partition query on top of that. That's strictly more RU cost and latency
for data that's never useful on its own. Embedding wins outright here.

**When you WOULD reference instead:** imagine adding a `Product` catalog, and each
`OrderLine` pointing at a product by `ProductId` instead of embedding the product's full
details. Products are a genuinely different shape of data from order lines:
- **Large.** A full product record — description, images, specs, categories — is much
  bigger than the three or four fields an order line actually needs. Copying all of that
  into every order that ever sells the product bloats every `Order` document with data the
  order itself has no use for.
- **Shared.** Thousands of orders reference the same product. Embedding it means a single
  price correction or description fix would, in principle, need to rewrite every
  historical order that ever referenced it — or, if you deliberately *don't* rewrite
  history, embedding becomes a price-at-time-of-sale snapshot instead of a reference. That
  can be exactly what you want (a customer's receipt should show what they paid, not
  today's price) or exactly a bug (an out-of-date description on every past order), which
  is precisely why "embed or reference" has to be a deliberate call, not a default.
- **Independently queried and updated.** Browsing the catalog, adjusting stock levels, and
  editing a product's description are all operations that never need to touch a single
  order. A separate `products` container, referenced by id, lets those operations happen
  without reading or writing anything in `orders` at all.

**Your Task:**
Write down (a comment in your demo file is fine) which of the two designs — embed or
reference — you'd choose for a `Warehouse` associated with an `OrderLine` (the warehouse an
item ships from), and justify it using the same three questions above: is it large, is it
shared, is it independently queried/updated?

**Questions to think about:**
1. Order lines embed cleanly because they're small, unshared, and never queried alone.
   Which of those three properties would have to change before embedding them stopped
   making sense?
2. If `Product` were referenced instead of embedded, what would an order confirmation
   screen need to do differently to show a product's name and image next to each line?

---

## Part 4: Schema Evolution Without Migrations

Cosmos has no schema to migrate — a container simply stores whatever JSON your code
writes to it. That's freeing (no migration script, no downtime window to add a column) and
dangerous in exactly the same breath: nothing stops two different versions of your code
from writing two different shapes of `Order` into the same container at the same time.

**Your Task:**
Add a `ShippingAddress` type and two new properties to `Order`:

```csharp
// Domain/ShippingAddress.cs
namespace CosmosModeling.Domain;

public class ShippingAddress
{
    public string Line1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
```

```csharp
// New properties on Order:
public int SchemaVersion { get; set; } = 2;
public ShippingAddress? ShippingAddress { get; set; }
```

**Your Task:**
Write `Demos/Part4SchemaEvolution.cs` that:
1. Writes an **old-shape** order directly as JSON — not via your `Order` class, so it's a
   document that genuinely never had `SchemaVersion` or `ShippingAddress` on the wire, not
   just a C# object with those fields left at their defaults:

```csharp
using Newtonsoft.Json.Linq;

var oldShapeDocument = new JObject
{
    ["id"] = oldOrderId,
    ["customerId"] = customerId,
    ["OrderDate"] = DateTimeOffset.UtcNow.AddMonths(-6).ToString("O"),
    ["Status"] = "Delivered",
    ["OrderLines"] = new JArray
    {
        new JObject { ["ProductId"] = "sku-1", ["ProductName"] = "Keyboard", ["Quantity"] = 1, ["UnitPrice"] = 79.99m },
    },
    // No SchemaVersion, no ShippingAddress -- this is the "before" shape.
};

using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(oldShapeDocument.ToString()));
using var createResponse = await orders.CreateItemStreamAsync(stream, new PartitionKey(customerId));
```

2. Reads it back through **today's** `Order` type and prints `SchemaVersion` (should be
   `0` — the C# default, since the field was never in the JSON) and whether
   `ShippingAddress` is `null`.
3. Writes a **new** order using the current `Order` class (with `ShippingAddress` set) and
   confirms it round-trips with `SchemaVersion == 2`.

**Why it deserializes instead of throwing:** Newtonsoft.Json (the serializer
`Microsoft.Azure.Cosmos` uses by default) simply leaves a C# property at its default value
when the JSON has no matching field — `0` for `int`, `null` for a nullable reference type.
No exception, no migration, no schema check. That's exactly why your *reading* code has to
handle both shapes on purpose:

```csharp
var shippingLine = order.ShippingAddress is { } address
    ? $"{address.Line1}, {address.City}"
    : "(no shipping address on file -- this order pre-dates that field)";
```

**The `SchemaVersion` convention:** a plain `int` property you bump by hand whenever a
change to a document's shape is significant enough that reader code needs to branch on it
deliberately — not for every trivial optional field, but for changes where "is this field
present" isn't a reliable enough signal on its own (for instance, a field that's legally
allowed to be `null` even in new documents). A reader can then say "if `SchemaVersion < 2`,
this predates `ShippingAddress` — go look it up some other way" instead of guessing from
which fields happen to exist.

**Questions to think about:**
1. If you renamed `Status` to `OrderStatus` in a new version of `Order`, would old
   documents' `Status` values still be readable by any code? What would you have to do in
   your reading code to bridge old and new documents through a rename (as opposed to an
   added field, which is what this part covered)?
2. Why does this problem barely exist in module 10's PostgreSQL schema? (Hint: think about
   what `ALTER TABLE ADD COLUMN` does to every existing row, and who enforces that every
   row in a table has the same columns.)

---

## Part 5: A Synthetic/Composite Partition Key

Part 2 flagged the trade-off: partitioning `Order` by `CustomerId` alone means one very
high-volume customer's orders all live in a single, ever-growing logical partition. This
part builds the fix for that specific scenario.

**Your Task:**
Create `Domain/HighVolumeOrder.cs`:

```csharp
using Newtonsoft.Json;

namespace CosmosModeling.Domain;

public class HighVolumeOrder
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string CustomerId { get; set; } = string.Empty;
    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;
    public List<OrderLine> OrderLines { get; set; } = [];

    // The synthetic partition key itself, stored as a real property -- Cosmos partition
    // keys always come from an actual JSON property, never a computed/virtual one.
    [JsonProperty("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;

    public static string BuildPartitionKey(string customerId, DateTimeOffset orderDate) =>
        $"{customerId}:{orderDate:yyyy-MM}";
}
```

Add a third container to `AppHost/Program.cs`:

```csharp
database.AddContainer("orders-highvolume", "/partitionKey");
```

...and a matching `EnsureHighVolumeOrdersContainerAsync` to `CosmosInitializer`.

**Your Task:**
Write `Demos/Part5SyntheticPartitionKey.cs` that:
1. Writes three `HighVolumeOrder`s for the same customer across three different months
   (e.g. July, August, September 2026), each with `PartitionKey` built from
   `HighVolumeOrder.BuildPartitionKey(customerId, orderDate)`.
2. Queries for just September's orders by setting `PartitionKey` on the request options to
   that month's synthetic key, and confirms only September's order(s) come back.

**Why this bounds partition growth:** instead of every order this customer ever places
sharing one `CustomerId`-keyed partition, each month gets its own partition
(`"cust-123:2026-07"`, `"cust-123:2026-08"`, ...). A customer who places 50,000 orders a
year is now spread across 12 partitions of roughly 4,000 orders each, rather than one
partition holding all 50,000 forever.

**The cost, stated as plainly as the benefit:** "give me ALL of this customer's orders,
all time" is no longer a single-partition query — Cosmos has no way to know, from the
`CustomerId` alone, which of potentially many month-partitions to look in. You'd either
fan out across every month partition that customer has ever used (a cross-partition query,
same cost profile as never having partitioned by customer at all) or maintain a separate
index of which months exist for that customer. This is a genuine trade, made deliberately,
and **only** for the customers active enough to actually need it — an ordinary customer
should stay on Part 2's plain `Order`/`/customerId` design, which keeps "all of this
customer's orders" cheap precisely because you're not paying Part 5's cost for no reason.

**Questions to think about:**
1. Why is year-month a reasonable bucket size here, rather than, say, year-day or
   year-only? What would each alternative do to the number of partitions an "all of this
   customer's history" query would have to fan out across?
2. Could you apply this same synthetic-key technique to the *ordinary* `Order` container
   from Part 2, for every customer, not just high-volume ones? What would that cost you for
   the 99% of customers who never needed it?

---

## Reflection Questions

After completing this exercise, answer these:

1. **Every partition key choice in this exercise (`/id`, `/customerId`, the synthetic
   key) optimizes for one specific access pattern.** For each of the three, name the query
   it makes cheap, and one query it makes *more* expensive as a result.
2. **Embedding vs. referencing**: state the general rule you'd apply to a new piece of
   data you haven't seen before, using the three questions from Part 3 (size, sharing,
   independent access).
3. **Why can two documents in the same Cosmos container have completely different sets of
   properties, while two rows in the same PostgreSQL table cannot?** What enforces
   consistency in each system, if anything does?
4. **The `SchemaVersion` field is entirely a convention** — Cosmos does nothing with it
   automatically. Why write it at all, instead of just checking `if (order.ShippingAddress
   is null)` everywhere you need to know if a field might be missing?
5. **Which of this exercise's five parts has no equivalent at all in module 10's
   relational model**, and why not?

---

## Summary

You've learned:
- Choosing `/id` as a partition key for point-lookup-dominated documents
- Choosing `/customerId` for documents dominated by "give me this parent's children"
  queries, and the partition-growth trade-off that comes with it
- Embedding related data that's always accessed together, versus referencing data that's
  large, shared, or independently queried
- Reading and writing documents of different shapes from the same container, and managing
  that deliberately with a `SchemaVersion` convention
- Building a synthetic, composite partition key to bound growth for a high-cardinality
  scenario a single property can't handle alone

## Next Steps
- Compare your work against [`solution/`](solution/)
- Run the test suite: `dotnet test tests` (needs Docker — see `GETTING_STARTED.md`)
- Move on to **CosmosQuerying** — the LINQ provider and the parameterized SQL API against
  this same domain, seeded with far more data, plus cross-partition query cost in more
  detail than Part 2 touched on here
