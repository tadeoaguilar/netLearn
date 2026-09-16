# Getting Started with CosmosConsistencyAndTransactions

## Quick Start Guide

### 1. Start the Cosmos DB Emulator
Everything in this project needs the emulator running. Either:
```bash
# From the repository root -- starts the emulator via Aspire AND launches
# solution/ wired to it:
dotnet run --project 11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/AppHost
```
or start it by hand and work against it directly:
```bash
docker run -p 8081:8081 -p 10250-10255:10250-10255 --name cosmos-emulator \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```
The first start is slow (a multi-minute pull + boot) -- this is a heavier
image than module 10's Postgres container.

### 2. Navigate to the Workspace
```bash
cd 11-NoSqlCosmosDb/CosmosConsistencyAndTransactions/CosmosConsistencyAndTransactions
```

### 3. Verify the Project Setup
```bash
dotnet build
```
You should see a successful build message. Your `appsettings.json` is
already configured to point at `https://localhost:8081` with the
emulator's well-known default key (public, documented by Microsoft --
safe to commit, and it only ever authenticates against your local
emulator).

### 4. Project Structure

Your workspace starts nearly empty:
```
CosmosConsistencyAndTransactions/
├── CosmosConsistencyAndTransactions.csproj
├── Program.cs              # You'll build this out following EXERCISE.md
├── appsettings.json         # Already points at the local emulator
└── (folders you'll create)
    ├── Domain/              # Part 0: Customer, Order, CustomerLoyaltyProfile
    └── Demos/               # Parts 1-5: one file per part
```

### 5. Follow the Exercise
Open [EXERCISE.md](EXERCISE.md) and follow the step-by-step instructions,
in order:
1. **Part 0**: The domain documents and wiring -- including WHY
   `CustomerLoyaltyProfile` exists as a separate, denormalized document
2. **Part 1**: The five consistency levels
3. **Part 2**: Per-request overrides and session tokens
4. **Part 3**: ETag-based optimistic concurrency
5. **Part 4**: `TransactionalBatch`
6. **Part 5**: Cross-partition limitations and workarounds

### 6. Running Your Code
After each part, run it to see the results:
```bash
dotnet run
```

### 7. Comparing Against the Reference Solution
A complete, working implementation lives in `../solution/`, organized as
one demo file per part under `solution/Demos/`:
```bash
dotnet run --project ../solution -- 1     # Part 1 only
dotnet run --project ../solution -- all   # every part, in order
```

### 8. Running the Tests
```bash
dotnet test ../tests
```
This needs Docker running -- the tests boot the same `AppHost` project
in-process via `Aspire.Hosting.Testing`, emulator included, so you don't
need to `dotnet run` the AppHost separately first.

### 9. Common Commands
```bash
dotnet build      # Build the project
dotnet run        # Run the project
dotnet clean       # Clean build artifacts
dotnet restore     # Restore dependencies
```

### 10. Troubleshooting

**Connection refused / timeouts?**
- Confirm the emulator container is actually running:
  `docker ps` should show it.
- The emulator's first start can take several minutes -- give it time
  before assuming it's broken.

**`CosmosException` about certificates?**
- The emulator uses a self-signed certificate. `appsettings.json`
  already includes `DisableServerCertificateValidation=True` in the
  connection string for exactly this reason -- don't remove it for local
  work.

**Build errors mentioning Newtonsoft.Json?**
- `Microsoft.Azure.Cosmos` requires an explicit `Newtonsoft.Json` package
  reference in this SDK version. It's already in the `.csproj` -- if
  you're adding new files, make sure you're not fighting a stale
  `obj/`/`bin/` folder (`dotnet clean` then rebuild).

**Namespace errors?**
- Check your namespace matches the folder structure: `Domain/` files use
  `namespace CosmosConsistencyAndTransactions.Domain;`, `Demos/` files use
  `namespace CosmosConsistencyAndTransactions.Demos;`.

### 11. Need Help?
If you get stuck:
1. Check the error message carefully -- Cosmos SDK exceptions usually
   name the exact HTTP status code and sub-status.
2. Review the exercise instructions and the "Questions to think about"
   for the part you're on.
3. Compare your code against `../solution/`.
4. Ask Claude for guidance!

---

**Ready to start?** Open [EXERCISE.md](EXERCISE.md) and begin with Part 0!

Good luck!
