# Getting Started with GraphTransactions

## Quick Start Guide

### 1. Start Neo4j
```bash
cd 13-GraphDatabaseNeo4j
docker compose up -d neo4j-transactions
```
This starts a `neo4j:5-community` container named
`netlearn-graphtransactions-neo4j`, reachable at `bolt://localhost:7691`
(Bolt/driver protocol) and `http://localhost:7478` (Neo4j Browser), with
credentials `neo4j` / `graphtransactions`.

Give it a few seconds to finish starting, then confirm it's healthy:
```bash
docker compose ps neo4j-transactions
```

### 2. Navigate to the Workspace Project
```bash
cd GraphTransactions/GraphTransactions
```

### 3. Verify the Project Setup
```bash
dotnet build
```
You should see a successful build message.

### 4. Project Structure

Your workspace should look like this to start:
```
GraphTransactions/
├── GraphTransactions.csproj    # Project file with dependencies
├── Program.cs                  # Entry point (you'll edit this)
├── appsettings.json            # Points at bolt://localhost:7691
└── (folders you'll create)
    ├── Domain/                 # Part 0: Person, Company
    ├── Data/                   # Part 0: GraphSeeder
    ├── Demos/                  # Parts 1, 2, 3, 4
    └── Services/               # Part 5: ReferralService
```

### 5. Follow the Exercise

Open [EXERCISE.md](EXERCISE.md) and follow the step-by-step instructions.

The exercise is divided into 6 parts (Part 0 through Part 5):
1. **Part 0**: Domain types, an idempotent seeder, wiring up the driver
2. **Part 1**: Implicit transactions -- `ExecuteWriteAsync` is already atomic
3. **Part 2**: Explicit transactions -- the three-write referral scenario
4. **Part 3**: The cross-database contrast with Cosmos DB (no new code --
   a reading/reasoning part)
5. **Part 4**: Concurrent updates and Neo4j's default locking behavior
6. **Part 5**: The `ReferralService` unit-of-work wrapper

### 6. Running Your Code

After each part, run your code to see the results:
```bash
dotnet run
```
Once Part 0's `Program.cs` dispatches by part number (see the reference
`solution/Program.cs` for the pattern), you can target one part at a time:
```bash
dotnet run -- 1
dotnet run -- 2
dotnet run -- 4
```

### 7. Tips for Success

1. **Create folders as you go**: the exercise mentions files like
   `Domain/Person.cs` and `Demos/Part1ImplicitTransactionDemo.cs` -- create
   the folders first:
   ```bash
   mkdir Domain Data Demos Services
   ```
2. **Create files in your IDE**: use VS Code or your preferred editor to
   create the `.cs` files mentioned in the exercise.
3. **Type the code yourself**: don't copy-paste. Typing helps you learn.
4. **Run the console output, don't just read the code**: Part 4's locking
   demo is much clearer once you see "Writer B: holding the lock..." print
   before "Writer A: committed." in your own terminal.
5. **Ask questions**: if something doesn't make sense, that's normal --
   think through it or ask for help.

### 8. Common Commands

```bash
# Build the project
dotnet build

# Run the project
dotnet run

# Run one part
dotnet run -- 2

# Clean build artifacts
dotnet clean

# Restore dependencies
dotnet restore

# Run the reference solution
dotnet run --project ../solution -- all

# Run the test suite (needs Docker running -- it starts its own container)
dotnet test ../tests
```

### 9. Troubleshooting

**Build errors?**
- Make sure you're in the `GraphTransactions/GraphTransactions` directory
  (note: nested folder, same layout as this module's other projects)
- Run `dotnet restore`

**Can't connect to Neo4j?**
- Confirm the container is running: `docker compose ps neo4j-transactions`
  from `13-GraphDatabaseNeo4j/`
- Confirm `appsettings.json` points at `bolt://localhost:7691` with
  `neo4j` / `graphtransactions` -- these are unique to THIS project; the
  module's other Neo4j projects use different ports and passwords (see the
  module's `docker-compose.yml`)

**`dotnet test` hangs or fails to start a container?**
- Confirm Docker Desktop (or your Docker daemon) is actually running --
  `tests/` uses Testcontainers to start its own disposable Neo4j instance,
  completely separate from the `docker compose` container above

**Namespace errors?**
- Check your namespace matches the folder structure (e.g.
  `GraphTransactions.Domain`, `GraphTransactions.Services`)
- Ensure you have `using` statements for `Neo4j.Driver` where needed

### 10. Need Help?

If you get stuck:
1. Check the error message carefully
2. Review the exercise instructions
3. Compare against `solution/` for the part you're on
4. Make sure files are in the correct folders
5. Ask Claude for guidance!

---

**Ready to start?** Open [EXERCISE.md](EXERCISE.md) and begin with Part 0!

Good luck!
