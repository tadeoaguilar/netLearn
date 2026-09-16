# Getting Started with CosmosIndexingAndThroughput

## Quick Start

### 1. Make Sure Docker Is Running
The `AppHost` starts the Cosmos DB emulator itself -- there's nothing to start by hand. You
just need Docker Desktop (or your Docker daemon) running *before* you run the AppHost or the
tests. The emulator image is heavy; budget a few minutes for its first pull and start.

### 2. Navigate to This Project
```bash
cd 11-NoSqlCosmosDb/CosmosIndexingAndThroughput
```

### 3. Verify the Build (No Docker Needed for This)
```bash
dotnet build
```
Building never touches Docker -- only *running* the app or the tests does.

### 4. What Is Already Here

```
CosmosIndexingAndThroughput/
├── README.md                # The concepts behind each part
├── EXERCISE.md              # The work, in 6 parts (0 through 5)
├── GETTING_STARTED.md       # This file
│
├── CosmosIndexingAndThroughput/  # <- YOUR WORKSPACE. Write your code here.
│   ├── CosmosIndexingAndThroughput.csproj  #   ready to build
│   ├── Program.cs             #   replace as you work through the exercise
│   ├── appsettings.json       #   already points at the emulator's well-known endpoint/key
│   ├── Models/                #   create as you go: Customer, Order, OrderLine
│   ├── Data/                  #   create as you go: DataSeeder, SampleOrderFactory
│   ├── Indexing/               #   create as you go: IndexingPolicyFactory, IndexingWalkthrough
│   ├── Metrics/                #   create as you go: CosmosQueryExtensions, RequestChargeDemo
│   ├── Throughput/              #   create as you go: ThroughputWalkthrough
│   └── Retry/                    #   create as you go: RetryPolicy, RetryWalkthrough
│
├── solution/                # <- REFERENCE IMPLEMENTATION. Look after trying.
│   └── (same shape as above, fully implemented)
│
├── AppHost/                 # .NET Aspire orchestration
│   └── Program.cs             # starts the emulator, provisions containers, runs `solution`
│
└── tests/                   # ~20 tests, against the real emulator
```

The workspace's `.csproj` and `appsettings.json` already exist and build as-is, so you can
start typing immediately.

### 5. The Commands You Need

```bash
# Run your own work directly against a manually-started emulator (see step 7 below) --
# no Aspire, no Docker orchestration, just your code and appsettings.json
dotnet run --project CosmosIndexingAndThroughput

# Run via Aspire -- starts the emulator, waits for it, then runs solution/ wired to it
dotnet run --project AppHost

# Check your work against the tests (needs Docker -- boots the AppHost in-process)
dotnet test tests

# See the reference solution run through all five parts in order
dotnet run --project solution
```

### How to Use the Reference Solution

`solution/` uses the same namespaces and type names as `EXERCISE.md`, so you can compare
your file against its counterpart directly.

Attempt each part yourself first. Open the reference when you're stuck, or once you've
finished a part and want to compare approaches -- reading it up front is the fastest way to
feel productive and learn nothing.

The tests point at `solution/`'s types out of the box (via a `ProjectReference` chain
through `AppHost`). To exercise **your** code instead, you'd need to point that same
reference (and the `using CosmosIndexingAndThroughput.Indexing;` / `.Retry;` / etc.
statements the tests rely on) at your workspace project instead -- the tests will fail to
compile until your types exist with matching names, which makes them a usable checklist for
how far you've got.

### 6. Two Ways to Run This Project

**Via Aspire (recommended -- matches how the tests work):**
```bash
dotnet run --project AppHost
```
This starts the Cosmos DB emulator in a container, waits for it to report healthy, wires
its real connection string into `solution` (overriding `appsettings.json`), and runs it.
The Aspire dashboard URL is printed on startup.

**Standalone, against a manually-started emulator:**
```bash
docker run -p 8081:8081 -p 10250-10255:10250-10255 --name cosmos-emulator \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```
Wait for the container's logs to report it's ready (this can take a minute or two), then:
```bash
dotnet run --project CosmosIndexingAndThroughput   # or: dotnet run --project solution
```
`appsettings.json` already points at `https://localhost:8081` with the emulator's
well-known default key -- nothing else to configure.

### 7. About the Tests and Docker

The test suite uses [`Aspire.Hosting.Testing`](https://learn.microsoft.com/dotnet/aspire/testing/write-your-first-test),
not a manually-started emulator -- `dotnet test tests` boots the same `AppHost` project
in-process, emulator included, once for the whole run (see `tests/CosmosFixture.cs`), and
shares it across every test class that needs it. That means:
- `dotnet test tests` needs **Docker running**, but does **not** need you to
  `dotnet run --project AppHost` or start an emulator by hand first.
- The very first test in a run pays the emulator's full cold-start cost (a few minutes) --
  this is expected, not a hang.
- The retry/backoff tests (`RetryPolicyTests`) don't touch the emulator at all -- they
  construct a fake `CosmosException` directly, so they run instantly even before Docker has
  finished starting anything.

### 8. Start Exercising
Open [EXERCISE.md](EXERCISE.md) and begin with **Part 0: Project Setup**.

## Reading RU Cost While You Work

Every response from the Cosmos SDK carries a `RequestCharge` -- print it constantly from
Part 1 onward. This exercise's claims about what's cheap and what's expensive are only
convincing once you've watched the numbers yourself, on your own seeded data:

```csharp
var response = await container.ReadItemAsync<Order>(id, new PartitionKey(customerId));
Console.WriteLine($"{response.RequestCharge} RU");

var feedResponse = await iterator.ReadNextAsync();
Console.WriteLine($"{feedResponse.RequestCharge} RU for {feedResponse.Count} item(s)");
```

## What You've Learned So Far

### From CosmosModeling and CosmosQuerying
- How to shape a document and choose a partition key from its access pattern
- How to query documents with the SQL API and LINQ, including cross-partition query cost

### Now: CosmosIndexingAndThroughput
- **What** gets indexed by default, and what that costs on writes to a document with a
  large embedded array
- **How** to change an indexing policy after the fact, and why a composite index is
  required for a multi-property `ORDER BY`
- **What** each shape of operation (point read, single-partition query, cross-partition
  query, write) actually costs in RUs, measured rather than assumed
- **How** manual and autoscale throughput differ, and where each is configured when the
  Aspire hosting API doesn't expose it
- **When** to write your own retry logic on top of the SDK's default 429 handling, and when
  the default is already enough

## Tips for Success

### 1. Compare RU Numbers Before and After Part 2's Policy Change
The write-cost savings from excluding `/orderLines/*` and the query-cost change for the
now-unindexed path are the whole point of Part 2 -- don't just apply the new policy and move
on, actually look at both `RequestCharge` values side by side.

### 2. Seed Enough Data to See a Difference
A single-partition query and a cross-partition query cost about the same on a container
with five documents. The dataset in this exercise is deliberately ~120 orders across ~20
customers so the cross-partition fan-out in Part 3 is actually visible.

### 3. Don't Expect to See a Real 429 Against the Emulator
Part 5's manual retry path is very unlikely to fire from a single client hitting the
emulator under normal exercise conditions -- that's expected, not something to debug. The
retry *logic* itself is what's tested directly, not real throttling.

## Common Mistakes

### "I forgot Docker was running" / `dotnet run --project AppHost` hangs at startup
Aspire needs a running Docker daemon to start the emulator container. Start Docker Desktop
first, then re-run.

### "The emulator is still starting" -- timeouts on the first request
A multi-minute first start is normal. `AppHost` and the test fixture both wait for the
resource to report `Running` before proceeding, with a 3-minute timeout in the tests.

### "ORDER BY ... requires a composite index" even after Part 2
Double-check the composite index's paths and sort orders match the query **exactly** --
`ORDER BY customerId ASC, orderDate DESC` needs a composite index of
`(customerId Ascending, orderDate Descending)` in that order; a mismatched sort direction
or property order will not satisfy it.

### `Microsoft.Azure.Cosmos` fails to build with a Newtonsoft.Json targets error
It requires an explicit `Newtonsoft.Json` `PackageReference` in any project that references
it directly. Every `.csproj` here that references `Microsoft.Azure.Cosmos` already has this.

### `dotnet test` hangs or fails immediately
`Aspire.Hosting.Testing` still needs Docker running, even though you're not calling
`docker run` yourself. If it's running and tests still seem stuck, give it the full 3
minutes the fixture allows before assuming it's hung.

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

### 1. Indexing Is a Write-Cost/Read-Cost Trade, Not a Free Default
Every indexed path costs something on every write. The default policy indexes everything
because it doesn't know your query patterns; you do, and customizing the policy is how you
tell Cosmos what you actually need.

### 2. RequestCharge Is the Closest Thing Cosmos Has to `EXPLAIN ANALYZE`
It's deterministic for a given operation against a given container state, and it's the only
honest way to compare two strategies -- guessing from documentation numbers means guessing
about your own data.

### 3. Throughput Mode Is a Cost Decision the AppHost Doesn't Make For You
Manual and autoscale are both valid; picking between them (and configuring either) is a
client-SDK-side decision made against your actual traffic pattern, not something Aspire's
hosting integration decides on your behalf.

### 4. The SDK's Retry Is a Floor, Not a Ceiling
Built-in 429 retry handles surviving an occasional throttle silently. It does not give you
visibility into *why* you're being throttled sustainedly -- that's what your own retry logic
is for, when you need it.

## After Completing This Project

You'll understand:
- Cosmos DB's default indexing policy and how to customize it deliberately
- Why and how to add a composite index for a multi-property `ORDER BY`
- How to measure and compare `RequestCharge` across different operation shapes
- The manual vs. autoscale throughput trade-off, and where to configure each via the SDK
- When to add manual retry/backoff logic on top of the Cosmos SDK's built-in 429 handling

## Next Steps

1. Complete Parts 0 through 5
2. Compare against `solution/`
3. Run `dotnet test tests` and confirm everything passes (needs Docker)
4. If you're working through module 11 in order, move to **CosmosConsistencyAndTransactions**

---

**Ready to see what your documents actually cost?** Open [EXERCISE.md](EXERCISE.md) and
start with Part 0!
