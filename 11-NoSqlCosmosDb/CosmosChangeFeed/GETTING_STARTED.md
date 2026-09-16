# Getting Started with CosmosChangeFeed

## Quick Start Guide

### 1. Navigate to the Project
```bash
cd 11-NoSqlCosmosDb/CosmosChangeFeed/CosmosChangeFeed
```

### 2. Verify the Project Setup
```bash
dotnet build
```

You should see a successful build message.

### 3. Start the Cosmos DB Emulator

You have two options:

**Option A: Run via .NET Aspire (recommended)**
```bash
cd ../AppHost
dotnet run
```
Aspire starts the Cosmos DB Linux emulator in Docker, waits for it to be ready, creates the `customers`, `orders`, `customerordersummary`, and `leases` containers, and wires the connection string into the app automatically. **The first start is slow** -- the Cosmos emulator image is considerably heavier than, say, the Postgres emulator from earlier modules. Expect several minutes on a cold Docker image cache.

**Option B: Run a manually-started emulator**
If you already have the Azure Cosmos DB emulator running locally (see the module README for setup), `appsettings.json` in both `CosmosChangeFeed/` and `solution/` already points at its well-known default endpoint and key. You'll need to create the four containers yourself (via the emulator's Data Explorer or the SDK) since nothing does that for you outside of Aspire.

### 4. Project Structure

Your workspace should have this structure:
```
CosmosChangeFeed/
├── CosmosChangeFeed.csproj    # Project file with dependencies
├── Program.cs                  # Main entry point (you'll edit this)
├── appsettings.json             # Emulator connection string (already set up)
├── EXERCISE.md                 # Step-by-step exercise guide
├── GETTING_STARTED.md          # This file
└── (files you'll create)
    ├── Models.cs                # Order, OrderLine, CustomerOrderSummary
    └── OrderChangeHandler.cs    # The change feed handler
```

### 5. Follow the Exercise

Open [EXERCISE.md](EXERCISE.md) and follow the step-by-step instructions.

The exercise is divided into 6 parts:
1. **Part 1**: What the change feed is (and isn't) -- no code, just concepts
2. **Part 2**: Set up the change feed processor and lease container
3. **Part 3**: Build the materialized `CustomerOrderSummary` read model
4. **Part 4**: At-least-once delivery and idempotent handler design
5. **Part 5**: What happens when a handler throws -- no code, just concepts
6. **Part 6**: Put it all together and run the demo

### 6. Running Your Code

After each part, run your code to see the results:
```bash
dotnet run
```

**Be patient with the output.** Unlike a typical CRUD exercise where you see a result immediately, this one involves:
- The processor acquiring its lease(s) after `StartAsync()` (roughly 1-3 seconds on a warm emulator)
- The processor's own poll interval before it notices a new write
- The handler's own query back to `orders` to recompute the summary

If you write an order and immediately read the summary, you will very likely see nothing yet -- that's expected, not a bug. Poll with a timeout instead (the exercise shows you how).

### 7. Common Commands

```bash
# Build the workspace project
dotnet build

# Run the workspace project (needs a running emulator + containers)
dotnet run

# Run everything via Aspire (starts the emulator for you)
dotnet run --project ../AppHost

# Run the tests (needs Docker -- Aspire starts the emulator for the test run)
dotnet test ../tests

# Clean build artifacts
dotnet clean
```

### 8. Troubleshooting

**Build errors?**
- Make sure you're in the `CosmosChangeFeed/CosmosChangeFeed` directory (note: nested folder)
- Run `dotnet restore`

**"Missing ConnectionStrings:cosmos"?**
- Running standalone requires `appsettings.json` next to the built executable -- it's already configured to copy on build, but double-check you're running from the project directory, not some other working directory.

**Summary document never appears / test times out waiting for it?**
- Confirm the emulator is actually running (`docker ps` should show the Cosmos emulator container)
- Confirm all four containers exist: `customers`, `orders`, `customerordersummary`, `leases`
- Confirm the change feed processor actually started (`await processor.StartAsync()` before writing any orders) -- writes made before the processor starts are still picked up once it starts, but only after its first poll, so give it a moment
- Increase the poll timeout before assuming something is broken -- a cold emulator start can be slow

**Tests hang or time out?**
- The test suite needs Docker running locally; if there's no Docker daemon available in your environment, the tests will fail to start the emulator resource (this is expected in a sandboxed environment without Docker -- `dotnet build` is still the meaningful signal there)

### 9. Need Help?

If you get stuck:
1. Check the error message carefully
2. Review the exercise instructions, especially Part 1's discussion of *why* this is asynchronous
3. Check your container names match exactly: `orders`, `customerordersummary`, `leases`
4. Compare against `solution/` if you're really stuck
5. Ask Claude for guidance!

---

**Ready to start?** Open [EXERCISE.md](EXERCISE.md) and begin with Part 1!

Good luck!
