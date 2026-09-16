# EfCoreMigrations - Evolving a Schema with EF Core Migrations

## Overview
Every real application's schema changes after the first release. This
project is about that change: creating migrations, applying them, hand-editing
one when the generated default would silently lose data, adding an index and
a database-enforced constraint, and seeding reference data two different
ways. The domain is deliberately tiny -- an `Author`/`Book` schema with two
tables -- because the point is the *workflow*, not modeling complexity.

Unlike most modules in this repository, this one is PostgreSQL-only. The
check constraint in Part 4 and the identity-column behaviour in Part 5 rely on
real Postgres, not a SQLite stand-in.

## What You'll Learn

### The Migration Workflow
- **Creating migrations**: `dotnet ef migrations add <Name>` and what it
  scaffolds
- **Applying migrations**: `dotnet ef database update`,
  `context.Database.Migrate()`, and migration bundles
- **Rolling back**: `dotnet ef database update <PreviousMigration>` and
  `dotnet ef migrations remove`
- **Design-time context creation**: `IDesignTimeDbContextFactory<TContext>`,
  and why `dotnet ef` needs it

### Schema Evolution
- Adding a column (the easy case)
- Renaming a column (the case that looks easy and isn't) -- and why the
  generated migration deserves a second look before you trust it
- Adding an index and a Postgres CHECK constraint through the Fluent API

### Seeding Data
- `HasData` in `OnModelCreating`: declarative, versioned, diffed
  automatically by future migrations -- right for small, fixed reference
  tables
- An idempotent runtime seed (`INSERT ... ON CONFLICT`): right for data that
  isn't part of the schema's permanent shape

## Why This Matters

### The Migration You Don't Look At Is the One That Bites You
```csharp
// Renamed a C# property from PageCount to Pages, then ran
// `dotnet ef migrations add`. If the tool can't confirm it's a rename:
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "page_count", table: "books");
    migrationBuilder.AddColumn<int>(name: "pages", table: "books", ...);
    // Every existing book just lost its page count. Silently. In production.
}
```
The fix is one method call each way:
```csharp
protected override void Up(MigrationBuilder migrationBuilder) =>
    migrationBuilder.RenameColumn(name: "page_count", table: "books", newName: "pages");

protected override void Down(MigrationBuilder migrationBuilder) =>
    migrationBuilder.RenameColumn(name: "pages", table: "books", newName: "page_count");
```

### Two Kinds of Seed Data Need Two Different Tools
```csharp
// Fixed reference table -- versioned with the schema, diffed by future
// migrations automatically. Wrong tool for anything that changes often.
builder.HasData(new Genre { Id = 1, Name = "Fiction" }, /* ... */);

// Idempotent, runs every startup, no migration to write to change a value.
// Wrong tool for anything that needs to be part of the schema's history.
await context.Database.ExecuteSqlInterpolatedAsync($"""
    INSERT INTO authors (id, name) VALUES (1000, 'Demo Author')
    ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name
    """);
```

## Project Structure

```
EfCoreMigrations/
├── EfCoreMigrations/          # <- YOUR WORKSPACE. Build the schema here.
│   ├── EfCoreMigrations.csproj
│   ├── Program.cs
│   └── appsettings.json
│                                (no Migrations/ folder -- you create it)
│
├── solution/                  # <- REFERENCE IMPLEMENTATION.
│   ├── EfCoreMigrations.Solution.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Models/
│   │   ├── Author.cs
│   │   ├── Book.cs
│   │   └── Genre.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   ├── AppDbContextFactory.cs     # IDesignTimeDbContextFactory
│   │   ├── SnakeCaseNaming.cs
│   │   ├── SeedData.cs                # idempotent runtime seed
│   │   └── Configurations/
│   │       └── Configurations.cs      # Author, Book, Genre Fluent API
│   └── Migrations/                    # five real, generated migrations
│
├── tests/                     # <- Testcontainers-backed schema tests
│   ├── EfCoreMigrations.Tests.csproj
│   ├── PostgresFixture.cs
│   └── MigrationSchemaTests.cs
│
├── EXERCISE.md                # The work, in 6 parts
├── GETTING_STARTED.md
└── README.md                  # This file
```

## Quick Start

1. **Start Postgres** (from `10-EntityFrameworkCore/`, the module root):
   ```bash
   docker compose up -d
   ```

2. **Navigate:**
   ```bash
   cd EfCoreMigrations/EfCoreMigrations
   ```

3. **Verify:**
   ```bash
   dotnet build
   ```

4. **Start learning:**
   Open [EXERCISE.md](EXERCISE.md)

## The Learning Path

### Part 1: The Initial Model and Migration (30 min)
Model `Author`/`Book`, write the `DbContext`, and create `InitialCreate`.

### Part 2: Adding a Column (15 min)
`AddBookPageCount` -- the easy, purely additive case.

### Part 3: Renaming a Column, By Hand (30 min)
`RenameBookPagesColumn` -- the core teaching moment of this project.

### Part 4: Index and Check Constraint (25 min)
`AddIndexAndBookRatingConstraint` -- a lookup index and a database-enforced
range constraint, both through the Fluent API.

### Part 5: Two Ways to Seed Data (30 min)
`SeedGenreReferenceData` -- `HasData` for a fixed lookup table, contrasted
with an idempotent runtime seed for data that isn't.

### Part 6: Applying, Bundling, Rolling Back (20 min, discussion + practice)
`Database.Migrate()` vs. `dotnet ef database update` vs. migration bundles,
and how to roll back.

**Total Time**: 2.5-3 hours

## Key Takeaways

- A migration is code, not magic -- read what it generates before you trust
  it, especially around renames.
- `RenameColumn` and `DropColumn`+`AddColumn` look similar in a diff and are
  not remotely similar in effect.
- EF Core 7+'s Fluent API has first-class support for check constraints
  (`ToTable(t => t.HasCheckConstraint(...))`) -- reach for it before raw SQL.
- `HasData` is for data that is part of your schema's versioned shape.
  Anything else belongs in a runtime seed.
- `IDesignTimeDbContextFactory<TContext>` decouples `dotnet ef` from your
  application's hosting model -- useful the moment your `DbContext`
  constructor needs something a design-time process can't provide.

## Common Mistakes

- **Trusting the generated rename migration without reading it.** EF Core's
  scaffolder is good at detecting an unambiguous single-property rename, but
  "good at" is not "guaranteed to". Open every migration before you run
  `database update` against real data.
- **Seeding volatile data with `HasData`.** Every future `migrations add`
  diffs your `HasData` list against what the previous migration inserted --
  fine for six genres, painful for anything that changes weekly.
- **Forgetting `IDesignTimeDbContextFactory`.** Without it, `dotnet ef`
  falls back to finding your `Program.cs`'s host builder, which may need
  services or configuration a bare CLI invocation can't supply.
- **Applying migrations with `Database.Migrate()` inside a multi-instance
  deployment** without a leader-election or single-migrator strategy --
  concurrent `Migrate()` calls racing each other is exactly what migration
  bundles run under a controlled deployment step exist to avoid.

## Next Module
This is currently the last module. Revisit
[09-EnterpriseCRUD](../../09-EnterpriseCRUD/) and notice how much of its
`TaskManagement.Infrastructure/Persistence` layer -- including
`SnakeCaseNaming.cs`, copied verbatim into this project -- you can now
explain from first principles.
