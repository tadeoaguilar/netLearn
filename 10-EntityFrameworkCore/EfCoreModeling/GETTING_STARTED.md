# Getting Started with EfCoreModeling

## Quick Start Guide

### 1. Navigate to the Project
```bash
cd 10-EntityFrameworkCore/EfCoreModeling/EfCoreModeling
```

### 2. Verify the Project Setup
```bash
dotnet build
```

You should see a successful build message.

### 3. Project Structure

Your workspace should have this structure:
```
EfCoreModeling/
├── EfCoreModeling.csproj    # Project file with dependencies
├── Program.cs               # Entry point (you'll replace this)
├── appsettings.json         # Connection string (already set)
├── EXERCISE.md              # Step-by-step exercise guide
├── GETTING_STARTED.md       # This file
└── (folders you'll create)
    ├── Entities/            # Part 1 onward: Author, Book, Money, Isbn, ...
    ├── Configurations/      # Part 1 onward: one IEntityTypeConfiguration<T> per entity
    └── Persistence/         # Part 1: LibraryDbContext. Part 8: SnakeCaseNaming
```

### 4. Do You Need Docker or Postgres Running?

**No.** This is the one project in the module that doesn't need a
database at all. EF Core builds its model -- entities, relationships,
columns, indexes, everything you'll inspect in this exercise -- entirely
in memory, the first time something touches `context.Model`. `UseNpgsql`
only records a connection string; it doesn't connect.

You'll want Postgres running (`docker compose up -d` from
`10-EntityFrameworkCore/`) once you move on to `EfCoreMigrations` and
later projects, which do query a real database. Not here.

### 5. Follow the Exercise

Open [EXERCISE.md](EXERCISE.md) and follow the step-by-step instructions.

The exercise is divided into 8 parts:
1. **Part 1**: DbContext + your first entity (`Author`), Fluent API
2. **Part 2**: One-to-many relationships (`Publisher`, `Author` → `Book`)
3. **Part 3**: An owned type (`Money` as `Book.Price`)
4. **Part 4**: A value converter (`Isbn`)
5. **Part 5**: Many-to-many with a payload (`Book` ↔ `Genre` via `BookGenre`)
6. **Part 6**: Table-per-hierarchy inheritance (`DigitalBook`)
7. **Part 7**: Indexes and constraints (`Review`, unique index on `Isbn`)
8. **Part 8**: Snake-case naming, applied last

### 6. Running Your Code

After each part, run your code to see the model grow:
```bash
dotnet run
```

Each part's Program.cs prints every entity, its columns, and its
relationships -- watch new entries appear as you add each mapping.

### 7. Checking Your Work

Once you've completed a part (or all of them), point the test project at
your own code instead of the reference solution:

```bash
# In tests/EfCoreModeling.Tests.csproj, change:
<ProjectReference Include="../solution/EfCoreModeling.Solution.csproj" />
# to:
<ProjectReference Include="../EfCoreModeling/EfCoreModeling.csproj" />
```

Then:
```bash
dotnet test ../tests
```

The 46 tests need no database either -- they inspect `context.Model`
directly. Each failure names the exact mapping it expected (a foreign
key, an index, a column name), so it doubles as a checklist for what's
left to build.

### 8. Tips for Success

1. **Type the code yourself**: Don't copy-paste. Typing helps you learn!
2. **Run after every part**: The model grows incrementally -- watching it
   change is most of the point.
3. **Read the "why" boxes**: Each part explains the reasoning behind the
   mapping choice, not just the syntax.
4. **Compare against `solution/` once you're stuck or done**: It uses the
   same namespaces and type names as `EXERCISE.md`, so you can diff your
   file against its counterpart directly.

### 9. Common Commands

```bash
# Build the project
dotnet build

# Run the project (prints the configured model)
dotnet run

# Run the reference solution instead
dotnet run --project ../solution

# Check your work against the tests (after re-pointing the ProjectReference)
dotnet test ../tests

# Clean build artifacts
dotnet clean

# Restore dependencies
dotnet restore
```

### 10. Troubleshooting

**Build errors about a missing type?**
- Check that the file lives in the folder its namespace implies:
  `Entities/` → `EfCoreModeling.Entities`, `Configurations/` →
  `EfCoreModeling.Configurations`, `Persistence/` →
  `EfCoreModeling.Persistence`
- Add the matching `using` statement where the type is referenced

**`ApplyConfigurationsFromAssembly` doesn't seem to find your
configuration?**
- Confirm the class implements `IEntityTypeConfiguration<T>` for the
  right `T`, and that it's `public`

**A model-validation exception mentioning keys "mapped to the same table
but with different columns"?**
- This is almost always the owned-type/naming-convention interaction
  explained in Part 8 -- re-read the comment in `SnakeCaseNaming.cs`
  about the ownership-link shadow property. If you wrote your own naming
  convention rather than copying it exactly, check whether you're
  renaming every property on an owned type, including its shadow primary
  key.

**Tests still failing after you think you've matched the exercise?**
- Read the failing test's name and assertion message first -- each one
  names exactly what it expected (a column name, a foreign key, a
  constraint) via FluentAssertions' failure output
- Compare the relevant file against its counterpart in `../solution/`

**`appsettings.json` not found at run time?**
- It should already be set to copy to the output directory in
  `EfCoreModeling.csproj`:
  ```xml
  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
  ```

### 11. Need Help?

If you get stuck:
1. Check the error message carefully -- EF Core's model-validation errors
   are usually specific about which two things it thinks conflict
2. Review the relevant part of `EXERCISE.md`, especially the "why" after
   the code block
3. Compare your file against its counterpart in `../solution/`
4. Ask Claude for guidance!

---

**Ready to start?** Open [EXERCISE.md](EXERCISE.md) and begin with Part 1!

Good luck!
