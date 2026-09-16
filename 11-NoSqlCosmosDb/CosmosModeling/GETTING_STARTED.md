# Getting Started with CosmosModeling

## Quick Start

### 1. Make Sure Docker Is Running
Unlike module 10's `docker compose up -d`, there's nothing to start by hand here — the
`AppHost` starts the Cosmos DB emulator itself. You just need Docker Desktop (or your
Docker daemon) running *before* you run the AppHost or the tests. The emulator image is
noticeably heavier than module 10's Postgres image, so budget a few minutes for its first
pull and start, and a few hundred MB to a few GB of memory while it runs.

### 2. Navigate to This Project
```bash
cd 11-NoSqlCosmosDb/CosmosModeling
```

### 3. Verify the Build (No Docker Needed for This)
```bash
dotnet build
```
Building never touches Docker — only *running* the app or the tests does.

### 4. What Is Already Here

```
CosmosModeling/
├── README.md                # The concepts behind each part
├── EXERCISE.md              # The work, in 6 parts (0 through 5)
├── GETTING_STARTED.md       # This file
│
├── CosmosModeling/          # <- YOUR WORKSPACE. Write your code here.
│   ├── CosmosModeling.csproj  #   ready to build
│   ├── Program.cs             #   replace as you work through Part 0
│   ├── appsettings.json       #   already points at the emulator's well-known endpoint/key
│   ├── Domain/                #   create as you go: Customer, Order, OrderLine, ...
│   ├── Persistence/             #   create as you go: CosmosInitializer
│   └── Demos/                    #   create as you go: one demo per part
│
├── solution/                # <- REFERENCE IMPLEMENTATION. Look after trying.
│   └── (same shape as above, fully implemented)
│
├── AppHost/                 # .NET Aspire orchestration
│   └── Program.cs             # starts the emulator, provisions containers, runs `solution`
│
└── tests/                   # ~19 tests, against the real emulator
```

The workspace's `.csproj` and `appsettings.json` already exist and build as-is, so you can
start typing immediately.

### 5. The Commands You Need

```bash
# Run your own work directly against a manually-started emulator (see step 7 below) --
# no Aspire, no Docker orchestration, just your code and appsettings.json
dotnet run --project CosmosModeling

# Run via Aspire -- starts the emulator, waits for it, then runs solution/ wired to it
dotnet run --project AppHost

# Check your work against the tests (needs Docker -- boots the AppHost in-process)
dotnet test tests

# See the reference solution run, one part at a time, standalone
dotnet run --project solution -- 1      # The Customer document (/id)
dotnet run --project solution -- 2      # The Order document, embedded OrderLines (/customerId)
dotnet run --project solution -- 3      # Embedding vs. referencing
dotnet run --project solution -- 4      # Schema evolution without migrations
dotnet run --project solution -- 5      # A synthetic/composite partition key
dotnet run --project solution -- all    # Everything in order
```

### How to Use the Reference Solution

`solution/` uses the same namespaces and type names as `EXERCISE.md`, so you can compare
your file against its counterpart directly.

Attempt each part yourself first. Open the reference when you're stuck, or once you've
finished a part and want to compare approaches — reading it up front is the fastest way to
feel productive and learn nothing.

The tests point at `solution/`'s types out of the box (via a direct `ProjectReference` from
`tests/`). To exercise **your** code instead, you'd need to point that same
`ProjectReference` (and the `using CosmosModeling.Domain;` / `using
CosmosModeling.Persistence;` statements the tests rely on) at your workspace project
instead — the tests will fail to compile until your types exist with matching names, which
makes them a usable checklist for how far you've got.

### 6. Two Ways to Run This Project

**Via Aspire (recommended — matches how the tests work):**
```bash
dotnet run --project AppHost
```
This starts the Cosmos DB emulator in a container, waits for it to report healthy, wires
its real connection string into `solution` (overriding `appsettings.json`), and runs it.
The Aspire dashboard URL is printed on startup — open it to see the emulator resource,
logs, and traces.

**Standalone, against a manually-started emulator:**
```bash
docker run -p 8081:8081 -p 10250-10255:10250-10255 --name cosmos-emulator \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```
Wait for the container's logs to report it's ready (this can take a minute or two), then:
```bash
dotnet run --project CosmosModeling   # or: dotnet run --project solution
```
`appsettings.json` already points at `https://localhost:8081` with the emulator's
well-known default key — nothing else to configure. This path is useful when you want fast
edit/run cycles without Aspire's dashboard and orchestration overhead in the loop, or when
you're debugging something Aspire-unrelated.

### 7. About the Tests and Docker

The test suite uses [`Aspire.Hosting.Testing`](https://learn.microsoft.com/dotnet/aspire/testing/write-your-first-test),
not a manually-started emulator — `dotnet test tests` boots the same `AppHost` project
in-process, emulator included, once for the whole run (see
`tests/CosmosModelingFixture.cs`), and shares it across every test class. That means:
- `dotnet test tests` needs **Docker running**, but does **not** need you to
  `dotnet run --project AppHost` or start an emulator by hand first.
- The very first test in a run pays the emulator's full cold-start cost (a few minutes) —
  this is expected, not a hang. Every subsequent test in the same run reuses the same
  emulator instance and runs fast.
- Test runs never affect a manually-started emulator you might be using interactively.

### 8. Start Exercising
Open [EXERCISE.md](EXERCISE.md) and begin with **Part 0: Project Setup**.

## Reading RU Cost While You Work

Every response from the Cosmos SDK carries a `RequestCharge` — the number of Request
Units that operation cost. Print it as you go; it's the fastest way to build intuition for
why a single-partition query (Part 2) costs less than a cross-partition one, and why a
point read (Part 1) is the cheapest operation Cosmos offers:

```csharp
var response = await container.ReadItemAsync<Customer>(id, new PartitionKey(id));
Console.WriteLine($"{response.RequestCharge} RU");

var feedResponse = await iterator.ReadNextAsync();
Console.WriteLine($"{feedResponse.RequestCharge} RU for {feedResponse.Count} item(s)");
```

## What You've Learned So Far

### From Module 10 (EfCoreModeling, EfCoreMigrations, EfCoreQuerying)
- Normalized relational schemas, foreign keys, and joins
- Schema migrations as an explicit, versioned, database-enforced process
- Querying efficiently within a fixed, database-enforced schema

### Now: CosmosModeling
- **What** a document actually is: whatever shape your code writes, no schema enforced by
  the database at all
- **How** a partition key decision shapes which queries are cheap and which are expensive
  — a decision relational databases mostly hide from you behind an index the query planner
  picks
- **Why** embedding and referencing are both legitimate tools, and how to tell which one a
  given relationship needs
- **What** "no migrations" really means in practice: freedom from a migration script, and
  full responsibility for handling every shape your container has ever held

## Tips for Success

### 1. Print RequestCharge Constantly
See "Reading RU Cost While You Work" above. Do this from Part 1 onward — RU cost is the
closest thing Cosmos has to Postgres's `EXPLAIN ANALYZE`, and this exercise's claims about
what's cheap and what's expensive are only convincing once you've watched the numbers
yourself.

### 2. Don't Skip Writing the Old-Shape Document as Raw JSON in Part 4
It's tempting to just leave `ShippingAddress` unset on an `Order` instance and call that
"the old shape" — but that only proves a C# object with a null property deserializes fine,
which was never in doubt. Writing the raw JSON directly (no `ShippingAddress` key on the
wire at all) is what actually proves the document itself never had the field, which is the
real scenario Part 4 is about.

### 3. Think in Partitions, Not Just in Types
Two `Order` documents with the exact same shape can still behave completely differently
depending on their `CustomerId` — one might share a partition with 3 other orders, another
with 30,000. The type system doesn't see this at all; only the partition key does.

## Common Mistakes

### "I forgot Docker was running" / `dotnet run --project AppHost` hangs at startup
Aspire needs a running Docker daemon to start the emulator container. Start Docker Desktop
(or your Docker daemon) first, then re-run. There's no useful error message if Docker
simply isn't there yet — it just never progresses past "Starting".

### "The emulator is still starting" — timeouts on the first request
The Cosmos DB emulator is a genuinely heavy container; a **multi-minute** first start is
normal, not a bug. `AppHost` and the test fixture both wait for the resource to report
`Running` before proceeding (`WaitForResourceAsync(..., KnownResourceStates.Running)`,
with a 3-minute timeout in the tests) — if you see a timeout, try again once, and consider
whether your machine is under enough load that 3 minutes genuinely wasn't enough.

### "CosmosException: PartitionKey ... does not match"
The C# property (or its `[JsonProperty]`-mapped name) that the container's partition key
path points at doesn't match what actually ended up in the document's JSON. Partition key
paths are matched against the JSON on the wire, not your C# property names — if a
container's partition key path is `/customerId`, your property must serialize to exactly
`customerId` (check for a missing `[JsonProperty("customerId")]`, and remember Cosmos
partition key paths are case-sensitive).

### "Microsoft.Azure.Cosmos" fails to build with a Newtonsoft.Json targets error
`Microsoft.Azure.Cosmos` requires an explicit `Newtonsoft.Json` `PackageReference` in any
project that references it directly — it's not pulled in transitively in a way MSBuild can
resolve on its own. Every `.csproj` in this project that references
`Microsoft.Azure.Cosmos` already has this; if you add a new project, add both.

### `ASPIRECOSMOSDB001` build warning/error
This is Aspire's experimental-API diagnostic for `.WithDataExplorer()` on the Cosmos
emulator resource. This project deliberately doesn't call it — stick with plain
`.RunAsEmulator()` in `AppHost/Program.cs`.

### `dotnet test` hangs or fails immediately
`Aspire.Hosting.Testing` still needs Docker running to start the emulator, even though
you're not calling `docker run` yourself. If Docker Desktop isn't running, start it first.
If it's running and tests still seem stuck, give it the full 3 minutes the fixture allows
before assuming it's actually hung.

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

### 1. The Partition Key Is the Single Most Important Modeling Decision
More than the shape of any individual document, the partition key determines which
queries are cheap forever and which are expensive forever. Choose it based on your
dominant, real access pattern — not based on what feels like the "natural" primary key.

### 2. A Document's Shape Is a Property of Your Code, Not the Database
Nothing stops you from writing two different shapes of the same logical entity into one
container. That's not a bug to guard against defensively everywhere — it's a normal
consequence of shipping code over time, and it's why Part 4 exists.

### 3. Embedding Trades Query Simplicity for Update Fan-Out
Embedding avoids extra round trips at read time by duplicating (or co-locating) data at
write time. That's a great trade for data that's small and rarely updated independently
(order lines); a bad one for data that's large, shared, and updated often (a product
catalog).

### 4. Aspire Orchestrates; It Doesn't Own the App
Every project in this module runs unmodified against a manually-started emulator. Aspire's
`AppHost` exists to make local development and testing convenient, not to become a runtime
dependency your app can't function without.

## After Completing This Project

You'll understand:
- How to choose a Cosmos DB partition key from a document's real access pattern, and state
  the trade-off that choice implies
- When to embed related data and when to reference it, using a repeatable set of
  questions rather than a gut feeling
- How to write code that survives a document's shape changing over time, with no
  migration step to lean on
- How a synthetic, composite partition key solves a scaling problem a single property
  can't
- How .NET Aspire orchestrates a local emulator dependency for both interactive runs and
  automated tests

## Next Steps

1. Complete Parts 0 through 5
2. Compare against `solution/`
3. Run `dotnet test tests` and confirm everything passes (needs Docker)
4. Move to **CosmosQuerying**: the LINQ provider and the parameterized SQL API against this
   same domain, with a closer look at cross-partition query cost

---

**Ready to model some documents?** Open [EXERCISE.md](EXERCISE.md) and start with Part 0!
