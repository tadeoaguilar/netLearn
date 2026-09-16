# EfCoreQuerying - Querying with EF Core and PostgreSQL

## Overview
Modeling a schema and running migrations gets you a database. This project
is about getting data back out of it well: filtering, sorting, paging,
projecting instead of over-fetching, eager loading only when you need it,
computing aggregates in the database instead of in memory, and reaching for
raw SQL (safely) when LINQ genuinely isn't the right tool. Everything runs
against real PostgreSQL, so you'll also use three query features that don't
exist on other providers: `ILIKE`, native array columns, and JSONB.

## What You'll Learn

### Core Querying
- **Filtering, Sorting, Paging**: `Where`, `OrderBy`, `Skip`, `Take`
- **Projections**: `Select` into DTOs instead of loading full entities
- **Tracking**: `AsNoTracking()` and why read-only queries should use it
- **Eager Loading**: `Include`/`ThenInclude` vs. projecting related data
- **Split Queries**: `AsSplitQuery()` and the cartesian-explosion problem
- **Aggregates**: `GroupBy` with `Average`/`Count`, translated to SQL

### The Sharp Edges
- **Client vs. Server Evaluation**: recognizing LINQ EF Core can't translate,
  and why it throws instead of silently downloading your whole table
- **Raw SQL**: `FromSqlInterpolated`/`ExecuteSqlInterpolatedAsync`, and why
  interpolation (not string concatenation) is what makes them safe

### PostgreSQL-only Features
- `EF.Functions.ILike` for case-insensitive search
- Querying a native `text[]` array column
- Querying a `jsonb` column with `EF.Functions.JsonContains`

### Performance
- **Compiled Queries**: `EF.CompileAsyncQuery` for a hot, parameterized path,
  and when the added complexity is actually worth it

## Why This Matters

### Real-World Scenarios

**Projection over full entities**:
```csharp
// Loads every column of Book, Author, and Publisher, and tracks all of it --
// wasteful for a page that only renders title, author name, and price.
var books = await context.Books.Include(b => b.Author).Include(b => b.Publisher).ToListAsync();

// Loads exactly the columns the list page needs, tracks nothing.
var books = await context.Books
    .Select(b => new BookSummaryDto(b.Id, b.Title, b.Author.Name, b.Publisher.Name, b.Price, b.PublishedOn))
    .ToListAsync();
```

**The untranslatable-LINQ trap**:
```csharp
// Throws InvalidOperationException -- EF Core can't turn an arbitrary C#
// method call into SQL, and refuses to silently pull the whole table into
// memory to run it there instead.
var matches = await context.Books.Where(b => SomeLocalHelper(b.Title)).ToListAsync();
```

**Safe raw SQL**:
```csharp
// The {threshold} placeholder becomes a real ADO.NET parameter -- not string
// concatenation, so user input can never restructure the query.
var cheap = await context.Books.FromSqlInterpolated($"select * from books where price < {threshold}").ToListAsync();
```

## Project Structure

```
EfCoreQuerying/
├── EfCoreQuerying/          # <- YOUR WORKSPACE. Write your code here.
│   ├── EfCoreQuerying.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Domain/               # empty, waiting for your entities
│   ├── Persistence/           # empty, waiting for your DbContext
│   ├── Seed/                  # empty, waiting for your seeder
│   ├── Dtos/                  # empty, waiting for your projection DTOs
│   ├── Queries/                # empty, waiting for your compiled queries
│   └── Demos/                  # empty, waiting for your per-part demos
├── solution/                # <- REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Domain/                Author, Publisher, Genre, Book, Review
│   ├── Persistence/            LibraryDbContext, SnakeCaseNaming
│   ├── Seed/                   LibrarySeeder (deterministic, ~90 books)
│   ├── Dtos/                   projection DTOs
│   ├── Queries/                 compiled queries
│   └── Demos/                   one runnable demo per part
├── EXERCISE.md
├── GETTING_STARTED.md
└── README.md
```

## Quick Start

1. **Start PostgreSQL** (from `10-EntityFrameworkCore/`, the parent of this project):
   ```bash
   docker compose up -d
   ```

2. **Navigate:**
   ```bash
   cd EfCoreQuerying
   ```

3. **Verify:**
   ```bash
   dotnet build
   ```

4. **Start learning:**
   Open [EXERCISE.md](EXERCISE.md)

## The Learning Path

### Part 0: Domain Model, DbContext, and Seed Data (40 min)
Set up the schema and ~90 books of deterministic seed data everything else
queries against.

### Part 1: Basic LINQ (20 min)
`Where`/`OrderBy`/`Skip`/`Take` — filtering, sorting, paging.

### Part 2: Projections vs. Tracked Entities (25 min)
`Select` into DTOs, `AsNoTracking()`, and what tracking actually costs.

### Part 3: Include/ThenInclude vs. Projection (30 min)
Eager loading vs. projecting related data directly, and when each wins.

### Part 4: AsSplitQuery (25 min)
The cartesian-explosion problem with multiple collection `Include`s.

### Part 5: GroupBy and Aggregates (25 min)
Average rating per book, book count per genre — computed in SQL.

### Part 6: Client vs. Server Evaluation (20 min)
The classic gotcha: a LINQ expression EF Core can't translate to SQL.

### Part 7: Raw SQL (30 min)
`FromSqlInterpolated` composed with LINQ, `ExecuteSqlInterpolatedAsync` for
a bulk update, and why interpolation (not concatenation) is mandatory.

### Part 8: PostgreSQL-only Features (30 min)
`ILIKE`, array columns, JSONB — the queries you can't do on SQLite.

### Part 9: Compiled Queries (20 min)
`EF.CompileAsyncQuery` for a hot path, and when it's worth the complexity.

**Total Time**: 4-5 hours

## Best Practices

### Projections
✅ Default to `Select` into a DTO for read-only queries
✅ Use `AsNoTracking()` when you need entity shape but not tracking
❌ Don't `Include` related entities you're only going to read one field from

### Raw SQL
✅ Always use `FromSqlInterpolated`/`ExecuteSqlInterpolatedAsync` with a C#
   interpolated string
✅ Compose further LINQ onto `FromSqlInterpolated` results when useful
❌ Never use `FromSqlRaw` with concatenated or `string.Format`-ted user input

### Split Queries
✅ Use `AsSplitQuery()` when multiple collection `Include`s would duplicate
   rows
❌ Don't reach for it on a single collection `Include` — there's nothing to
   split

### Compiled Queries
✅ Reserve for genuinely hot, fixed-shape query paths
❌ Don't compile a query whose shape needs to vary per call (optional filters)

## Testing Considerations

The tests in `../tests` run against real PostgreSQL via
[Testcontainers](https://testcontainers.com/) — a fresh, throwaway container
per test run, so `dotnet test` needs Docker but not `docker compose up`
first. This matters specifically for this project: SQLite or the in-memory
provider simply cannot execute `ILIKE`, array operators, or `jsonb`
containment, so testing the Postgres-only parts against anything but real
Postgres would test nothing.

## Next Steps

After completing EfCoreQuerying:

1. **Review your query choices against real code you've written** — which
   of your past `Include` chains could have been a projection?
2. **Move to EfCoreTransactions**
   - [../EfCoreTransactions](../EfCoreTransactions/)
   - Explicit transactions, optimistic concurrency, and isolation levels

## Checklist

After this project, you should be able to:

- [ ] Filter, sort, and page results with `Where`/`OrderBy`/`Skip`/`Take`
- [ ] Choose between projecting to a DTO and loading tracked entities
- [ ] Explain what `AsNoTracking()` saves, and when to skip it
- [ ] Choose between `Include`/`ThenInclude` and projection for related data
- [ ] Recognize when `AsSplitQuery()` is needed and why
- [ ] Compute aggregates with `GroupBy` in the database, not in memory
- [ ] Recognize an untranslatable LINQ expression before EF Core tells you
- [ ] Write safe raw SQL with `FromSqlInterpolated`/`ExecuteSqlInterpolatedAsync`
- [ ] Use `ILIKE`, array columns, and JSONB from LINQ
- [ ] Decide when a compiled query is worth its added rigidity

---

**Ready to query?** Open [EXERCISE.md](EXERCISE.md)!
