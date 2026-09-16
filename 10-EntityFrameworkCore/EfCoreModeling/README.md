# EfCoreModeling - Modeling a Domain with EF Core's Fluent API

## Overview
This project teaches you how to model a real domain with EF Core's Fluent
API against PostgreSQL. You'll build a small library catalog -- authors,
publishers, genres, books, and reviews -- and along the way learn the
mapping techniques that come up in almost every EF Core codebase: one-to-
many relationships, an owned value object, a value converter, an explicit
many-to-many with a payload column, table-per-hierarchy inheritance,
indexes and constraints, and a snake_case naming convention applied across
the whole schema.

Nothing here requires a running database. EF Core builds its model the
first time something touches `context.Model`, and that step never opens a
connection -- which is also why the test project needs no Docker and no
Postgres to run.

## What You'll Learn

### Core Concepts
- **Fluent API over Data Annotations**: keeping entities as plain,
  persistence-ignorant POCOs
- **One-to-Many Relationships**: explicit foreign keys and delete behavior
- **Owned Types**: modeling a value object (`Money`) that has no identity
  of its own
- **Value Converters**: mapping a validated wrapper type (`Isbn`) to a
  primitive column
- **Explicit Many-to-Many**: a join entity (`BookGenre`) with its own
  payload column, instead of EF Core's implicit skip-navigation shortcut
- **Table-Per-Hierarchy Inheritance**: `DigitalBook` sharing a table with
  `Book`, distinguished by a discriminator column
- **Indexes and Constraints**: a unique index, a database-level check
  constraint, and required/max-length strings
- **Naming Conventions**: applying snake_case across an entire model in
  one pass, instead of one property at a time

### Practical Skills
- Writing `IEntityTypeConfiguration<T>` classes and wiring them up with
  `ApplyConfigurationsFromAssembly`
- Reading EF Core's model metadata (`context.Model`) directly, the same
  way this project's tests do
- Recognizing when an implicit many-to-many stops being enough
- Choosing between table-per-hierarchy and table-per-type for an
  inheritance hierarchy
- Deciding when a value converter is worth its trade-offs

## Project Structure

```
EfCoreModeling/
├── EfCoreModeling/                 # ← YOUR WORKSPACE. Write your code here.
│   ├── EfCoreModeling.csproj       #   ready to build
│   ├── Program.cs                  #   replace as you work through each part
│   └── appsettings.json            #   points at the efcore_modeling database
│
├── solution/                       # ← REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Entities/                   #   Author, Publisher, Genre, Book, DigitalBook,
│   │                               #   Review, BookGenre, Money, Isbn
│   ├── Configurations/             #   one IEntityTypeConfiguration<T> per entity
│   ├── Persistence/                #   LibraryDbContext, SnakeCaseNaming
│   ├── appsettings.json
│   └── Program.cs                  #   prints the configured model
│
├── tests/                          # ← 46 tests proving the model, no database needed
│   ├── RelationshipTests.cs
│   ├── OwnedTypeAndConversionTests.cs
│   ├── InheritanceTests.cs
│   ├── ConstraintTests.cs
│   ├── NamingTests.cs
│   └── TestDbContextFactory.cs
│
├── EXERCISE.md                     # Step-by-step exercise guide, 8 parts
├── GETTING_STARTED.md              # Quick start instructions
└── README.md                       # This file
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK (pinned in the repository's `global.json`)
- Docker is **not** required for this project -- see
  [10-EntityFrameworkCore/README.md](../README.md) for the module-wide
  `docker compose up -d` you'll want for later projects, but nothing here
  needs it

### Quick Start

1. **Navigate to the project:**
   ```bash
   cd 10-EntityFrameworkCore/EfCoreModeling/EfCoreModeling
   ```

2. **Verify setup:**
   ```bash
   dotnet build
   ```

3. **Read the getting started guide:**
   Open [GETTING_STARTED.md](GETTING_STARTED.md)

4. **Start the exercise:**
   Open [EXERCISE.md](EXERCISE.md) and follow Part 1

### The Learning Path

**Follow this sequence:**

1. **Part 1: DbContext and Your First Entity** (25 minutes)
   - Configure `Author` with Fluent API instead of data annotations
   - Wire up `ApplyConfigurationsFromAssembly`

2. **Part 2: One-to-Many Relationships** (25 minutes)
   - `Publisher → Book` and `Author → Book`
   - Explicit foreign keys and delete behavior

3. **Part 3: An Owned Type for Price** (20 minutes)
   - `Money` as a value object, mapped with `OwnsOne`

4. **Part 4: A Value Converter for Isbn** (25 minutes)
   - A validated wrapper type mapped to a `string` column

5. **Part 5: Many-to-Many with a Payload** (30 minutes)
   - `Book ↔ Genre` via the explicit `BookGenre` join entity
   - Why the implicit skip-navigation shortcut isn't enough here

6. **Part 6: Table-Per-Hierarchy Inheritance** (25 minutes)
   - `DigitalBook : Book`, sharing a table with a discriminator column

7. **Part 7: Indexes and Constraints** (25 minutes)
   - A unique index on `Isbn`, a check constraint on `Rating`

8. **Part 8: Snake-Case Naming, Applied Last** (20 minutes)
   - One convention, applied once, renaming the entire schema

**Total Time**: ~3 hours

## Key Takeaways

After completing this project, you should be able to:

✅ Explain why Fluent API keeps entities persistence-ignorant in a way
   data annotations cannot
✅ Configure a one-to-many relationship explicitly, including its delete
   behavior
✅ Model a value object as an EF Core owned type
✅ Map a validated wrapper type with a value converter, and describe its
   trade-offs against a bare primitive
✅ Recognize when a many-to-many needs an explicit join entity instead of
   EF Core's implicit skip-navigation shortcut
✅ Configure table-per-hierarchy inheritance with a discriminator column
✅ Add a unique index and a database-level check constraint
✅ Apply a naming convention across an entire model in one place

## Examples Covered

### Fluent API, Not Data Annotations
```csharp
// Entity stays a plain POCO -- no EF Core reference at all:
public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// The mapping lives in its own configuration class instead:
public class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
    }
}
```

### Owned Type
```csharp
public class Book
{
    public Money Price { get; set; } = new(0m, "USD");
}

public record Money(decimal Amount, string Currency);

// Configuration:
builder.OwnsOne(b => b.Price, price =>
{
    price.Property(p => p.Amount).HasPrecision(10, 2);
    price.Property(p => p.Currency).HasMaxLength(3);
});
```

### Explicit Many-to-Many with a Payload
```csharp
public class BookGenre
{
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
    public DateTime AddedAt { get; set; }   // the payload
}

// Configuration:
builder.HasKey(bg => new { bg.BookId, bg.GenreId });
```

### Table-Per-Hierarchy
```csharp
public class DigitalBook : Book
{
    public double FileSizeMb { get; set; }
    public string Format { get; set; } = string.Empty;
}

// Configuration, in BookConfiguration:
builder.HasDiscriminator<string>("BookType")
    .HasValue<Book>("book")
    .HasValue<DigitalBook>("digital_book");
```

## Testing Your Understanding

After completing the exercises, try to answer:

1. What's the real difference between `Book.Price` (an owned type) and
   `Book.Publisher` (a normal relationship)?
2. Why does the check constraint on `Review.Rating` belong in the
   database, even though the application validates it too?
3. When would you choose table-per-type over table-per-hierarchy for
   `DigitalBook`?
4. Why does `SnakeCaseNaming` have to run *after*
   `ApplyConfigurationsFromAssembly`, not before?
5. What does a value converter cost you, compared to mapping a primitive
   directly?

## Common Mistakes to Avoid

❌ Reaching for `[Required]`/`[MaxLength]` attributes instead of the
   Fluent API, which quietly couples entities to EF Core
❌ Forgetting `.OnDelete(...)` and getting EF Core's default cascade
   behavior when `Restrict` is what you actually want
❌ Using EF Core's implicit many-to-many shortcut and then discovering,
   too late, that there's nowhere to put a payload column
❌ Writing a check constraint's SQL against PascalCase column names
   instead of the snake_case names the naming convention will produce
❌ Calling a naming convention *before* `ApplyConfigurationsFromAssembly`,
   so it has nothing yet to rename

## Next Steps

After completing EfCoreModeling, continue your learning with:

1. **[EfCoreMigrations](../EfCoreMigrations/)** - Evolve this kind of
   model over time: creating, applying, hand-editing, and rolling back
   migrations
2. **[EfCoreQuerying](../EfCoreQuerying/)** - LINQ, projections,
   `Include`/`AsSplitQuery`, and PostgreSQL-specific querying
3. **[EfCoreTransactions](../EfCoreTransactions/)** - Explicit
   transactions, optimistic concurrency, and isolation levels
4. **[EfCoreLoggingAndHealthChecks](../EfCoreLoggingAndHealthChecks/)** -
   Interceptors, logging, and health checks

## Additional Resources

- [EF Core Fluent API Documentation](https://learn.microsoft.com/en-us/ef/core/modeling/)
- [EF Core Owned Types](https://learn.microsoft.com/en-us/ef/core/modeling/owned-entities)
- [EF Core Value Conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
- [EF Core Inheritance Mapping](https://learn.microsoft.com/en-us/ef/core/modeling/inheritance)

## Tips for Success

1. **Type the code yourself** - Don't copy-paste. Muscle memory helps
   learning.
2. **Run after each part** - `dotnet run` prints the model as it stands;
   watch it grow one part at a time.
3. **Read the "why" after each step** - The mapping calls are short; the
   reasoning behind them is the point.
4. **Point the tests at your own work once you're done** - Swapping the
   `ProjectReference` in `tests/EfCoreModeling.Tests.csproj` turns 46
   tests into a checklist.

---

**Ready to begin?** Open [GETTING_STARTED.md](GETTING_STARTED.md) to start your journey!
