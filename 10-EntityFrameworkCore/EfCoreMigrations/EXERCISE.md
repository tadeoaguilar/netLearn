# Exercise: Evolving a Schema with EF Core Migrations

## Overview
You're going to build a small `Author`/`Book` schema and evolve it through
five real migrations, in the order teams actually hit these problems: create
the schema, add a column, rename a column (and discover why that's more
dangerous than it looks), add an index and a database-enforced constraint,
and seed reference data two different ways. Then you'll apply, roll back, and
remove migrations from the command line.

This project needs a real PostgreSQL database. Start one from the module
root before you begin:
```bash
cd ../..          # back to 10-EntityFrameworkCore/
docker compose up -d
cd EfCoreMigrations/EfCoreMigrations
```

## Learning Goals
By completing this exercise, you will:
- Create, apply, and roll back EF Core migrations from the CLI
- Know why `dotnet ef` needs `IDesignTimeDbContextFactory<TContext>` and how
  to write one
- Understand exactly what a column rename does to a running database, and
  how to fix a migration that gets it wrong
- Add an index and a CHECK constraint through the Fluent API
- Choose between `HasData` and a runtime seed for a given kind of data
- Explain `Database.Migrate()` vs. `dotnet ef database update` vs. a
  migration bundle, and when you'd reach for each

---

## Part 1: The Initial Model and First Migration

### Step 1.1: The Domain

**Your Task:**
Create `Models/Author.cs`:

```csharp
namespace EfCoreMigrations.Models;

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<Book> Books { get; } = new();
}
```

Create `Models/Book.cs`:

```csharp
namespace EfCoreMigrations.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public int AuthorId { get; set; }
    public Author? Author { get; set; }
}
```

That's the whole schema for now -- two tables, one relationship. Everything
that follows is about *changing* this, not designing it.

### Step 1.2: The snake_case Convention

This module follows the same PostgreSQL convention as `09-EnterpriseCRUD`:
every column, key, foreign key and index gets renamed to snake_case, because
PostgreSQL folds unquoted identifiers to lower case and hand-written SQL
against a `PascalCase` schema is miserable.

**Your Task:**
Copy `Data/SnakeCaseNaming.cs` verbatim from
`../solution/Data/SnakeCaseNaming.cs` (it's also identical to
`09-EnterpriseCRUD/src/TaskManagement.Infrastructure/Persistence/SnakeCaseNaming.cs`
apart from the namespace). It's a `ModelBuilder` extension,
`UseSnakeCaseNames()`, that walks every entity, property, key, foreign key
and index at model-building time and lowercases/underscores the name.

### Step 1.3: Fluent API Configuration

**Your Task:**
Create `Data/Configurations/Configurations.cs`:

```csharp
using EfCoreMigrations.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreMigrations.Data.Configurations;

public class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable("authors");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
    }
}

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title).IsRequired().HasMaxLength(300);

        builder.HasOne(b => b.Author)
            .WithMany(a => a.Books)
            .HasForeignKey(b => b.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Step 1.4: The DbContext

**Your Task:**
Create `Data/AppDbContext.cs`:

```csharp
using EfCoreMigrations.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMigrations.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Applied LAST, so it renames whatever the configurations produced.
        modelBuilder.UseSnakeCaseNames();
    }
}
```

### Step 1.5: Why `dotnet ef` Needs Its Own Factory

`dotnet ef migrations add` doesn't run your `Program.cs`. It builds your
`DbContext` in a separate, minimal design-time process, which means it has no
access to your host's DI container, configuration pipeline, or anything else
`Program.cs` wires up. By default it *tries* to find a way anyway (it will
attempt to instantiate a host from your `Program.cs`), but that's fragile the
moment your setup does anything nontrivial. The reliable fix is to hand it an
explicit recipe.

**Your Task:**
Create `Data/AppDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EfCoreMigrations.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=efcore_migrations;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
```

`dotnet ef` finds this automatically: it scans the project assembly for any
`IDesignTimeDbContextFactory<TContext>` implementation and uses it in
preference to anything else. No registration, no configuration flag.

**Your Task:**
Create `appsettings.json` in the project root (it's already there and set to
copy to the output directory):

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=efcore_migrations;Username=postgres;Password=postgres"
  }
}
```

### Step 1.6: Generate the First Migration

**Run it:**
```bash
dotnet ef migrations add InitialCreate -o Migrations
```

This creates a `Migrations/` folder with three files:
- `<timestamp>_InitialCreate.cs` -- the `Up`/`Down` methods
- `<timestamp>_InitialCreate.Designer.cs` -- a snapshot EF uses internally
- `AppDbContextModelSnapshot.cs` -- the model's current state, so the *next*
  `migrations add` knows what changed

**Open the migration file and read it.** You should see `CreateTable` calls
for `authors` and `books`, a primary key on each, and a foreign key from
`books.author_id` to `authors.id`. Every identifier should already be
snake_case -- that's `UseSnakeCaseNames()` at work.

### Step 1.7: Apply It

**Run it:**
```bash
dotnet ef database update
```

This connects to the `efcore_migrations` database (from your connection
string) and runs the migration for real. Verify it worked:
```bash
dotnet ef migrations list
```
You should see `InitialCreate` with no "(pending)" marker.

**Questions to think about:**
1. What would happen if you ran `dotnet ef migrations add` again right now,
   without changing the model? (Try it, then `dotnet ef migrations remove`
   the empty one it creates.)
2. Why does the factory read `appsettings.json` from
   `Directory.GetCurrentDirectory()` rather than some fixed path?

---

## Part 2: Adding a Column

Additive changes are the easy case -- existing rows get a default, nothing
existing is touched.

**Your Task:**
Add a page count to `Book`:

```csharp
public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public int AuthorId { get; set; }
    public Author? Author { get; set; }

    public int PageCount { get; set; }
}
```

**Run it:**
```bash
dotnet ef migrations add AddBookPageCount -o Migrations
```

Open the generated file. It should be a single `AddColumn<int>` call in `Up`
and a single `DropColumn` in `Down` -- nothing else changed, so nothing else
is touched.

**Apply it:**
```bash
dotnet ef database update
```

---

## Part 3: Renaming a Column, By Hand

This is the core exercise in this project. Read it carefully even if the
tool does the right thing for you automatically -- the muscle memory matters
more than this one case.

### Step 3.1: The Problem

You want to rename `PageCount` to `Pages`. From EF Core's point of view, when
it compares your old model snapshot to the new one, a rename and a
"delete this property, add an unrelated one" look identical: one property
disappeared from `Book`, a different one appeared. EF Core's migration
scaffolder tries to detect the difference -- when there's exactly one
plausible match (same entity, compatible type, one property removed and one
added), it can emit `RenameColumn` directly. **But that detection is a
heuristic, not a guarantee.** With more than one simultaneous change, across
different tool versions, or in an interactive terminal where it asks you to
confirm and you answer without thinking, it's just as easy to get this
instead:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "page_count", table: "books");

    migrationBuilder.AddColumn<int>(
        name: "pages",
        table: "books",
        type: "integer",
        nullable: false,
        defaultValue: 0);
}
```

Read that carefully. `DropColumn` **deletes the column and every value in
it.** `AddColumn` then creates a brand new, empty column with a default.
Every book in your database just lost its page count, silently, and the
migration "succeeded" -- there's no error, no warning at apply time. This is
the single most common way a routine EF Core migration destroys production
data.

### Step 3.2: Rename the Property and Generate

**Your Task:**
Rename the property in `Models/Book.cs`:

```csharp
public int Pages { get; set; }
```

**Run it:**
```bash
dotnet ef migrations add RenameBookPagesColumn -o Migrations
```

**Open the generated file and check which one you got:**

- **If it already contains `migrationBuilder.RenameColumn(...)`** in both
  `Up` and `Down` -- EF Core's scaffolder detected the rename for you. Good;
  confirm it looks like the block below and move on.
- **If it contains `DropColumn` + `AddColumn`** -- replace both method
  bodies by hand with:

```csharp
protected override void Up(MigrationBuilder migrationBuilder) =>
    migrationBuilder.RenameColumn(
        name: "page_count",
        table: "books",
        newName: "pages");

protected override void Down(MigrationBuilder migrationBuilder) =>
    migrationBuilder.RenameColumn(
        name: "pages",
        table: "books",
        newName: "page_count");
```

Either way, the file you end up with should use `RenameColumn`, not
`DropColumn`/`AddColumn`. `RenameColumn` compiles down to Postgres's
`ALTER TABLE ... RENAME COLUMN ...`, which changes the column's name in the
system catalog and touches zero rows of data.

### Step 3.3: Apply and Verify

**Apply it:**
```bash
dotnet ef database update
```

If you want to see the data-loss version happen for real (in a database you
don't care about), you can reproduce it deliberately: revert to
`AddBookPageCount`, insert a book, hand-write a throwaway migration using
`DropColumn`+`AddColumn` instead of `RenameColumn`, apply it, and watch the
value disappear. `tests/MigrationSchemaTests.cs` in this project has an
automated version of exactly this check (`Renaming_page_count_to_pages_preserved_existing_data`)
if you'd rather read it than do it by hand.

**Questions to think about:**
1. Why is `RenameColumn` reversible with zero data loss in *either*
   direction, while `DropColumn`+`AddColumn` loses data only going forward?
2. What other model changes have this same "looks like two operations, is
   actually one" shape? (Hint: think about renaming a table, or changing a
   foreign key's target.)

---

## Part 4: Index and Check Constraint

**Your Task:**
Add a rating to `Book`:

```csharp
public int? Rating { get; set; }
```

It's nullable -- most existing books won't have a rating yet, and a
migration can't retroactively invent one.

Update `BookConfiguration` in `Data/Configurations/Configurations.cs`:

```csharp
public void Configure(EntityTypeBuilder<Book> builder)
{
    builder.ToTable("books", t => t.HasCheckConstraint(
        "ck_books_rating_range",
        "rating IS NULL OR (rating BETWEEN 1 AND 5)"));
    builder.HasKey(b => b.Id);

    builder.Property(b => b.Title).IsRequired().HasMaxLength(300);

    builder.HasOne(b => b.Author)
        .WithMany(a => a.Books)
        .HasForeignKey(b => b.AuthorId)
        .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(b => b.Title);
}
```

**Why the Fluent API instead of raw SQL?** Older guidance says "there's no
clean Fluent API surface for check constraints, drop to
`migrationBuilder.Sql(...)`." That's out of date: since EF Core 7,
`ToTable(t => t.HasCheckConstraint(name, sql))` is first-class. It gets
picked up by `dotnet ef migrations add` like anything else in your model, it
generates proper `AddCheckConstraint`/`DropCheckConstraint` migration
operations (both directions, for free), and it shows up if you ever inspect
the model at runtime. Raw SQL via `migrationBuilder.Sql(...)` still has its
place -- for anything Postgres-specific enough that EF Core's cross-provider
Fluent API has no concept of it at all -- but a check constraint isn't that
case anymore.

**Run it:**
```bash
dotnet ef migrations add AddIndexAndBookRatingConstraint -o Migrations
```

Open the generated file. You should see three operations in `Up`:
`AddColumn` for `rating`, `CreateIndex` for `ix_books_title`, and
`AddCheckConstraint` for `ck_books_rating_range` -- and the exact reverse,
in reverse order, in `Down`.

**Apply it:**
```bash
dotnet ef database update
```

**Verify the constraint is real**, not just a comment: try inserting a book
with `rating = 6` directly in `psql` (`docker compose exec postgres psql -U
postgres -d efcore_migrations`) and watch Postgres reject it with a
`check constraint "ck_books_rating_range" is violated` error.

---

## Part 5: Two Ways to Seed Data

### Step 5.1: A Fixed Reference Table with `HasData`

**Your Task:**
Create `Models/Genre.cs`:

```csharp
namespace EfCoreMigrations.Models;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

Add the relationship to `Book`:

```csharp
public int? GenreId { get; set; }
public Genre? Genre { get; set; }
```

Add `Genres` to `AppDbContext`:

```csharp
public DbSet<Genre> Genres => Set<Genre>();
```

Add the FK to `BookConfiguration`, and a new `GenreConfiguration` with
`HasData`, in `Data/Configurations/Configurations.cs`:

```csharp
// Inside BookConfiguration.Configure, after the Title index:
builder.HasOne(b => b.Genre)
    .WithMany()
    .HasForeignKey(b => b.GenreId)
    .OnDelete(DeleteBehavior.SetNull);

// A new class in the same file:
public class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.ToTable("genres");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).IsRequired().HasMaxLength(100);

        builder.HasData(
            new Genre { Id = 1, Name = "Fiction" },
            new Genre { Id = 2, Name = "Science Fiction" },
            new Genre { Id = 3, Name = "Fantasy" },
            new Genre { Id = 4, Name = "Non-Fiction" },
            new Genre { Id = 5, Name = "Biography" },
            new Genre { Id = 6, Name = "Mystery" });
    }
}
```

**Why `HasData` here?** Genres are a small, closed, rarely-changing set --
exactly the shape of data that belongs *in the schema's version history*.
`HasData` is declarative: you list the rows you want to exist, and every
future `dotnet ef migrations add` diffs that list against what the previous
migration inserted, scaffolding whatever `InsertData`/`UpdateData`/
`DeleteData` operations are needed to reconcile the two. Add a seventh genre
next month, run `migrations add`, and EF Core writes the `InsertData` call
for you. The tradeoff: it needs a stable, explicit key (`Id = 1`, not an
identity column picking its own value) so the differ has something to match
rows by across migrations.

**Run it:**
```bash
dotnet ef migrations add SeedGenreReferenceData -o Migrations
```

Open the generated file: `CreateTable` for `genres`, `AddColumn` for
`books.genre_id`, `CreateIndex` and `AddForeignKey` for the relationship, and
an `InsertData` call carrying all six genre rows as literal values baked into
the migration -- not a reference to your C# `HasData` call, an actual
`InsertData` operation with the data copied in. That's what "versioned"
means here: the migration file itself is the historical record.

**Apply it:**
```bash
dotnet ef database update
```

### Step 5.2: Data That Isn't Fixed -- an Idempotent Runtime Seed

Contrast that with data you want present in every environment but that isn't
part of the schema's permanent shape: a demo author and book for local
development, say. You don't want to write a migration every time you tweak
the demo title, and you don't want `migrations add` diffing it against
history. It belongs at startup instead.

**Your Task:**
Create `Data/SeedData.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace EfCoreMigrations.Data;

public static class SeedData
{
    public static async Task EnsureDemoDataAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        // Explicit IDs make this idempotent: running it on every startup
        // updates the same two rows instead of accumulating duplicates.
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO authors (id, name)
            VALUES (1000, 'Demo Author')
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name
            """, cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO books (id, title, author_id, pages, rating, genre_id)
            VALUES (1000, 'Demo Book', 1000, 250, 4, 1)
            ON CONFLICT (id) DO UPDATE SET
                title = EXCLUDED.title,
                pages = EXCLUDED.pages,
                rating = EXCLUDED.rating,
                genre_id = EXCLUDED.genre_id
            """, cancellationToken);
    }
}
```

Call it from `Program.cs` after `context.Database.MigrateAsync()`.

**Why `INSERT ... ON CONFLICT` instead of "check with LINQ, then `Add` if
missing"?** The upsert is one atomic statement. A "does a demo author exist?
if not, insert one" round trip is two statements with a gap between them --
if two instances of your app start at the same moment (exactly the scenario
a container orchestrator creates), both can see "no rows" and both try to
insert, and the second one throws a duplicate-key exception. `ON CONFLICT`
sidesteps the race entirely.

**Questions to think about:**
1. What would go wrong if you used `HasData` for the demo author/book
   instead of a runtime seed?
2. What would go wrong if you used a runtime seed for the genres instead of
   `HasData`? (Think about what `dotnet ef migrations add` would do on your
   *next* schema change.)

---

## Part 6: Applying, Bundling, and Rolling Back

No new migration in this part -- this is about the tools around the
migrations you've already built.

### Three Ways to Apply Migrations

**`context.Database.Migrate()` / `MigrateAsync()`** -- called from your own
application code, typically once at startup. Convenient for a single-instance
app or local development. Dangerous for a multi-instance deployment: if
three replicas all start at once and all call `Migrate()`, you get three
processes racing to apply the same migration (EF Core takes a database-level
lock to keep this from corrupting the schema, but the replicas that lose the
race still block on startup waiting for it, which is not what you want your
readiness probe to see).

**`dotnet ef database update`** -- a one-time, human-run (or CI-run) command,
separate from application startup. This is the usual default for anything
beyond a solo local project: migrations run once, as an explicit deployment
step, before the new application version starts taking traffic.

**`dotnet ef migrations bundle`** -- packages your entire migration history
plus the `ef` tooling into a single, self-contained native executable
(`efbundle` / `efbundle.exe`) that needs nothing installed on the target
machine -- no .NET SDK, no `dotnet-ef` tool, not even the project's source.
You build it once in CI (`dotnet ef migrations bundle --output efbundle`)
and ship the executable to production as a deployment artifact, alongside
the app itself. This is the shape most teams converge on: it turns "did
someone remember to run migrations, with the right tool version, against the
right connection string" into "run this one file", which is exactly the kind
of manual step you want removed from a deployment runbook.

### Rolling Back

To move the *database* backward to an earlier point in your migration
history:
```bash
dotnet ef database update RenameBookPagesColumn
```
This runs the `Down` method of every migration after `RenameBookPagesColumn`,
in reverse order, undoing them. It's why every `Down` method matters just as
much as `Up` -- a migration with a wrong or missing `Down` is a one-way door.

**Your Task:** Try it. Roll back to `AddBookPageCount`, run
`dotnet ef migrations list` and confirm the later ones show as pending
again, then roll forward:
```bash
dotnet ef database update AddBookPageCount
dotnet ef migrations list
dotnet ef database update
```

To remove a migration you haven't applied anywhere yet (you just generated
it, or you're still iterating on your model locally):
```bash
dotnet ef migrations remove
```
This deletes the most recent migration's files and reverts the model
snapshot, as if you'd never run `migrations add`. It refuses to do this for a
migration that's already been applied to the target database -- for that,
roll back with `database update <PreviousMigration>` first, *then* remove it.

**Your Task:** Add a throwaway migration (`dotnet ef migrations add Scratch
-o Migrations`), confirm it appears in `migrations list`, then remove it with
`dotnet ef migrations remove` without ever applying it. Confirm it's gone
from both the `Migrations/` folder and `migrations list`.

---

## Reflection Questions

1. **Why does a column rename need special handling, when adding or dropping
   a column doesn't?**

2. **What makes `HasData` a poor fit for data that changes often, even
   though nothing stops you from using it that way?**

3. **Why does `dotnet ef` need `IDesignTimeDbContextFactory` at all -- why
   can't it just always use your application's normal startup path?**

4. **In a team of five, working on the same feature branch, two people each
   add a migration independently. What goes wrong when both are merged, and
   how would you notice before it reaches production?**

5. **Why is a migration bundle's "no .NET SDK required on the target"
   property valuable specifically for a *production* deployment, when
   `dotnet ef database update` works fine on your own machine?**

---

## Summary

You've learned:
- ✅ Creating and applying migrations with `dotnet ef`
- ✅ `IDesignTimeDbContextFactory<TContext>` and why `dotnet ef` needs it
- ✅ The real difference between `RenameColumn` and `DropColumn`+`AddColumn`,
  and how to fix a migration that gets it wrong
- ✅ Adding an index and a CHECK constraint through the Fluent API
- ✅ `HasData` vs. an idempotent runtime seed, and when to reach for each
- ✅ `Database.Migrate()` vs. `dotnet ef database update` vs. migration
  bundles
- ✅ Rolling back with `database update <PreviousMigration>` and removing an
  unapplied migration with `migrations remove`

## Checking Your Work

Compare your `Migrations/` folder against `../solution/Migrations/` -- not
for byte-for-byte identical generated code (timestamps will differ, and your
scaffolder may or may not have auto-detected the rename), but for the same
five migrations doing the same things. Then:

```bash
dotnet test ../tests          # needs Docker; proves the resulting schema is correct
```

---

**You've completed the EfCoreMigrations exercise!** Move on to
[EfCoreQuerying](../EfCoreQuerying/) to put this schema to work with LINQ.
