# Getting Started with CosmosQuerying

## Quick Start

### 1. Navigate to This Project
```bash
cd 11-NoSqlCosmosDb/CosmosQuerying
```

### 2. Verify Setup
```bash
dotnet build CosmosQuerying
```

### 3. What Is Already Here

```
CosmosQuerying/
├── README.md                # The concepts behind each part
├── EXERCISE.md              # The work, in 6 parts (0 through 5)
├── GETTING_STARTED.md       # This file
│
├── CosmosQuerying/          # <- YOUR WORKSPACE. Write your code here.
│   ├── CosmosQuerying.csproj  #   ready to build
│   ├── Program.cs             #   replace as you work through Part 0
│   ├── appsettings.json       #   already points at the local emulator
│   ├── Domain/                #   empty, waiting for your entities
│   ├── Persistence/            #   empty, waiting for your serialization + init helpers
│   ├── Seed/                    #   empty, waiting for your seeder
│   ├── Dtos/                     #   empty, waiting for your projection DTOs
│   └── Demos/                     #   empty, waiting for your per-part demos
│
├── solution/                # <- REFERENCE IMPLEMENTATION. Look after trying.
│   └── (same shape as above, fully implemented)
│
├── AppHost/                 # .NET Aspire orchestration for the Cosmos DB emulator
│
└── tests/                   # ~20 tests, against the real emulator
```

The folders and `appsettings.json` already exist, so you can start typing
immediately rather than setting up scaffolding.

### 4. Running Against the Emulator

You have two ways to get a Cosmos DB emulator running:

**Option A -- via Aspire (recommended, matches how the tests run):**
```bash
dotnet run --project AppHost
```
This needs Docker. Aspire starts the Cosmos DB Linux emulator container,
waits for it to become healthy, creates the `cosmosquerying` database and
its two containers, and starts `solution` wired up to it automatically. The
**first** start is slow -- the Cosmos emulator image is considerably heavier
than, say, Postgres's -- expect a few minutes before it's ready. Watch the
Aspire dashboard URL printed in the console for status.

**Option B -- a manually-started emulator, running your own workspace code:**
If you already have the Azure Cosmos DB emulator running some other way
(the Windows emulator, or `docker run` directly) on `localhost:8081`,
`appsettings.json` already points at it with the well-known local emulator
key. In that case just:
```bash
dotnet run --project CosmosQuerying
```
Your workspace code creates the database/containers itself via
`CosmosInitializer.EnsureDatabaseAsync` (Part 0.3) -- no Aspire required.

### 5. The Commands You Need

```bash
# Run your own work (against a manually-started emulator)
dotnet run --project CosmosQuerying

# Run the full orchestrated app (starts the emulator for you -- needs Docker)
dotnet run --project AppHost

# Check your work against the tests (needs Docker -- see below)
dotnet test tests

# See the reference solution run, one part at a time, against a
# manually-started emulator
dotnet run --project solution -- 1      # The LINQ provider
dotnet run --project solution -- 2      # The SQL API with QueryDefinition
dotnet run --project solution -- 3      # Cross-partition vs. partition-scoped
dotnet run --project solution -- 4      # Pagination
dotnet run --project solution -- 5      # Projections
dotnet run --project solution -- all    # Everything in order
```

### How to Use the Reference Solution

`solution/` uses the same namespaces and type names as `EXERCISE.md`, so you
can compare your file against its counterpart directly.

Attempt each part yourself first. Open the reference when you're stuck or
when you've finished a part and want to compare approaches -- reading it up
front is the fastest way to feel productive and learn nothing.

The tests point at `solution/` (via `AppHost`) out of the box. To run them
against **your** code instead, edit `tests/CosmosQuerying.Tests.csproj`'s
`ProjectReference`s to point `AppHost` at your workspace project, or swap
the `Projects.CosmosQuerying_AppHost` type by pointing the AppHost's own
`ProjectReference` at `../CosmosQuerying/CosmosQuerying.csproj` instead of
`../solution/CosmosQuerying.Solution.csproj`.

### About the Tests and Docker

The test suite uses [.NET Aspire's testing support](https://learn.microsoft.com/dotnet/aspire/testing/overview)
(`Aspire.Hosting.Testing`), not a container you start yourself -- every
`dotnet test` run boots its own Cosmos DB emulator via the same `AppHost`
project `dotnet run --project AppHost` uses, waits for it to report
`Running`, seeds it, runs every test against that one instance, and tears it
down afterward. That means:
- `dotnet test tests` needs **Docker running**.
- The **first** test run is slow -- budget several minutes for the emulator
  to boot. The shared `CosmosFixture` (an `IAsyncLifetime` xUnit fixture)
  starts it exactly **once** for the whole test run, not once per test.
- Test runs never touch any emulator you started yourself for `dotnet run`.

### 6. Start Exercising
Open [EXERCISE.md](EXERCISE.md) and begin with **Part 0: Domain Model,
Serialization, Containers, and Seed Data** -- everything after it depends on
having containers and data to query.

## Watching Request Charges

`FeedResponse<T>.RequestCharge` is the number every part after Part 0 cares
about. Sum it across every page of a `FeedIterator<T>` to get the true RU
cost of a query, and print it as you experiment:

```csharp
double totalRu = 0;
while (iterator.HasMoreResults)
{
    var page = await iterator.ReadNextAsync();
    totalRu += page.RequestCharge;
}
Console.WriteLine($"{totalRu:0.00} RU");
```

## What You've Learned So Far

### From CosmosModeling (this module's first project)
- Document design: embedding vs. referencing, choosing a partition key
- The document shape this project's `Order`/`Customer` types build on

### Now: CosmosQuerying
- **How** to get data back out efficiently: the LINQ provider, parameterized
  SQL, partition-aware queries, pagination, and projections
- **Where** Cosmos's LINQ provider stops being like EF Core's, and why
- **What** a partition key choice actually costs or saves you at query time,
  measured in real RU

## Tips for Success

### 1. Run Every Part, Don't Just Read It
Part 3's RU cost difference and Part 4's continuation-token behavior are
things you can't fully appreciate from reading the code -- actually watching
`RequestCharge` change between a cross-partition and a partition-scoped
version of the same query is what makes it stick.

### 2. Fix the camelCase Setting First, Not Last
Part 0.2 is easy to skip past as boilerplate. Skip it and Part 1's LINQ
queries will silently return zero rows the moment you add a `Where` clause
-- a confusing failure mode to debug after the fact versus understanding it
up front.

### 3. Watch RequestCharge, Not Just Result Counts
Every demo and test in this project treats `RequestCharge` as a first-class
number to check, not an afterthought -- get in the habit of looking at it
every time you run a query during the exercise, not just in Part 5.

## Common Mistakes

### "My LINQ query returns zero results even though I know matching documents exist"
You're missing `linqSerializerOptions: CosmosSerialization.LinqOptions` on a
`GetItemLinqQueryable<T>()` call -- see Part 0.2. The query translator and
the document serializer are two independent settings that both need the
same naming policy.

### "CreateItemAsync throws a 400 Bad Request about the partition key"
The `PartitionKey` you pass to `CreateItemAsync`/`GetItemQueryIterator` must
match the container's configured partition key **value on that document**,
not just any string. For `orders`, that's the document's own `CustomerId`.

### "My cross-partition query returns fewer results than I expected"
Check whether you accidentally scoped it with a `PartitionKey` that doesn't
match every document you meant to include -- Part 3's whole point is that a
partition-scoped query only ever sees one partition's documents.

## Troubleshooting

### "Failed to establish connection" / timeouts talking to localhost:8081
The emulator either isn't running yet or hasn't finished booting. If you're
using Aspire (`dotnet run --project AppHost`), check the Aspire dashboard's
resource list for the `cosmos` resource's state; if you started it manually,
give it a few minutes on first start.

### `dotnet test` hangs or times out
The Cosmos DB emulator container needs Docker running, and first boot is
slow (see "About the Tests and Docker" above). If `WaitForResourceAsync`
throws a timeout, it most likely means the emulator container never reached
`Running` within the fixture's 3-minute window -- check Docker is actually
running.

### "appsettings.json not found" at runtime
Check the `.csproj` has:
```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## Key Concepts to Internalize

### 1. RequestCharge Is the Ground Truth
Response size and result count don't tell you what a query costs. Every
comparison in this exercise (Part 3, Part 5) is decided by
`FeedResponse<T>.RequestCharge`, not intuition.

### 2. Two Independent Naming Settings
The document serializer (`CosmosClientOptions.SerializerOptions`) and the
LINQ translator (`CosmosLinqSerializerOptions` passed to
`GetItemLinqQueryable<T>()`) must agree, but the SDK will not tell you if
they don't -- it just returns nothing.

### 3. The Partition Key Is a Query Hint, Not Just a Storage Detail
`QueryRequestOptions.PartitionKey` doesn't change what a query matches
logically -- it changes how many physical partitions Cosmos has to check to
answer it.

### 4. Interpolation Is the Safety Mechanism, Not a Style Choice
`QueryDefinition` + `WithParameter` parameterizes automatically. Building
the same-looking query string by hand does not. The API you call is what
matters.

## After Completing This Project

You'll understand:
- How Cosmos's LINQ provider differs from EF Core's, and where its limits are
- How to write Cosmos SQL that's both expressive and injection-safe
- How to reason about a query's RU cost before running it, and how to
  measure it after
- When a cross-partition query is the right tool, and when it's a sign your
  partition key choice needs revisiting
- How to page through large result sets without paying for the pages you
  already read

## Next Steps

1. Complete Parts 0 through 5
2. Compare against `solution/`
3. Run `dotnet test tests` and make sure everything passes
4. Move on to this module's other projects -- change feed, and the
   Cosmos-specific consistency/partitioning topics this project didn't cover

---

**Ready to level up your Cosmos queries?** Open [EXERCISE.md](EXERCISE.md)
and start with Part 0!
