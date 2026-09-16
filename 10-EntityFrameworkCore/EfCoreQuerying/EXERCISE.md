# Exercise: Querying with EF Core and PostgreSQL

## Overview
In this exercise, you'll build a small library-catalog database and write the
LINQ (and, where LINQ isn't the right tool, raw SQL) that a real read-heavy
application needs: filtering, paging, projections, eager loading, aggregates,
PostgreSQL-specific features, and a couple of the sharp edges that catch
people who learned EF Core against SQLite or in-memory providers.

Everything here runs against **real PostgreSQL** — some of what you'll see
(`ILIKE`, native arrays, JSONB) simply doesn't exist on other providers.

## Learning Goals
By completing this exercise, you will:
- Filter, sort, and page results with `Where`/`OrderBy`/`Skip`/`Take`
- Know when to project into a DTO with `Select` instead of loading full
  entities, and what `AsNoTracking()` buys you when you do load entities
- Choose between `Include`/`ThenInclude` and projecting related data directly
- Recognize the multi-`Include` cartesian-explosion problem and fix it with
  `AsSplitQuery()`
- Compute aggregates with `GroupBy`
- Recognize a LINQ expression EF Core can't translate to SQL, and fix it
- Drop into raw SQL safely with `FromSqlInterpolated`/`ExecuteSqlInterpolatedAsync`
  when LINQ isn't the right tool
- Use PostgreSQL-only querying features: `ILIKE`, array columns, JSONB
- Decide when a compiled query is worth the complexity it adds

---

## The Scenario

You're building the read side of a library catalog: `Author`, `Publisher`,
`Genre`, `Book`, and `Review`, seeded with about 90 books so filters and
aggregates have real data to chew on. Nobody is editing this data through
your code in this exercise — every query here is read-only, which is exactly
the case where the choices in this exercise (projections, tracking,
`Include` vs. `Select`) matter most.

---

## Part 0: The Domain Model, DbContext, and Seed Data

Before you can query anything, you need a schema and some data.

### Step 0.1: Create the Domain Model

**Your Task:**
Create `Domain/Author.cs`, `Domain/Publisher.cs`, `Domain/Genre.cs`, and
`Domain/Review.cs`:

```csharp
// Domain/Author.cs
namespace EfCoreQuerying.Domain;

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<Book> Books { get; set; } = [];
}
```

```csharp
// Domain/Publisher.cs
namespace EfCoreQuerying.Domain;

public class Publisher
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<Book> Books { get; set; } = [];
}
```

```csharp
// Domain/Genre.cs
namespace EfCoreQuerying.Domain;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Implicit many-to-many with Book -- EF Core generates the join table
    // (book_genre) for us since neither side needs a payload column.
    public List<Book> Books { get; set; } = [];
}
```

```csharp
// Domain/Review.cs
namespace EfCoreQuerying.Domain;

public class Review
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    /// <summary>1 to 5, enforced by a check constraint in OnModelCreating.</summary>
    public int Rating { get; set; }

    public string? Comment { get; set; }
}
```

Then `Domain/Book.cs` — this is the interesting one, with a native Postgres
array column and a JSONB column:

```csharp
// Domain/Book.cs
namespace EfCoreQuerying.Domain;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public DateOnly PublishedOn { get; set; }
    public decimal Price { get; set; }

    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;

    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;

    /// <summary>
    /// Maps to a native Postgres text[] column -- the Npgsql provider
    /// understands array CLR types out of the box, no value converter needed.
    /// </summary>
    public string[] Tags { get; set; } = [];

    /// <summary>
    /// Free-form attributes such as {"language":"en","pages":320}, stored as
    /// a native Postgres jsonb column, mapped here as plain text. A
    /// JsonDocument property would also map to jsonb, but it's IDisposable
    /// and needs a hand-written value comparer to behave correctly under
    /// change tracking -- storing the JSON text is simpler and just as
    /// queryable via EF.Functions.JsonContains and friends (Part 8).
    /// </summary>
    public string Metadata { get; set; } = "{}";

    public List<Review> Reviews { get; set; } = [];
    public List<Genre> Genres { get; set; } = [];
}
```

**Why:** `int` identity keys, plain reference navigations, and a `List<T>`
collection on both sides of `Book`↔`Genre` are enough for EF Core to infer a
full many-to-many mapping with zero configuration — that's what "convention
over configuration" buys you. The `Tags` and `Metadata` properties are the
two PostgreSQL-only column types you'll query in Part 8.

### Step 0.2: Copy the snake_case Naming Convention

PostgreSQL folds unquoted identifiers to lower case, so a schema generated
with EF Core's PascalCase defaults would force you to quote every single
column name in hand-written SQL. `EfCoreModeling` (the first project in this
module) and `09-EnterpriseCRUD` both solve this the same way: a small
`ModelBuilder` extension that walks the model once and renames everything.

**Your Task:**
Create `Persistence/SnakeCaseNaming.cs` by copying it verbatim from
`09-EnterpriseCRUD/src/TaskManagement.Infrastructure/Persistence/SnakeCaseNaming.cs`
(only the namespace changes, to `EfCoreQuerying.Persistence`). It's about 90
lines, so it isn't reproduced here — go read it once while you're there; it's
a genuinely useful pattern outside this exercise too.

### Step 0.3: Create the DbContext

**Your Task:**
Create `Persistence/LibraryDbContext.cs`:

```csharp
using EfCoreQuerying.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Persistence;

public class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(entity =>
        {
            entity.ToTable("authors");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Publisher>(entity =>
        {
            entity.ToTable("publishers");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("genres");
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(g => g.Name).IsUnique();
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.ToTable("books");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Title).IsRequired().HasMaxLength(300);
            entity.Property(b => b.Isbn).IsRequired().HasMaxLength(20);
            entity.HasIndex(b => b.Isbn).IsUnique();
            entity.Property(b => b.Price).HasColumnType("numeric(10,2)");

            entity.Property(b => b.Tags).HasColumnType("text[]");
            entity.Property(b => b.Metadata).HasColumnType("jsonb");

            entity.HasOne(b => b.Author)
                .WithMany(a => a.Books)
                .HasForeignKey(b => b.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Publisher)
                .WithMany(p => p.Books)
                .HasForeignKey(b => b.PublisherId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(b => b.Genres)
                .WithMany(g => g.Books)
                .UsingEntity(j => j.ToTable("book_genre"));
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Comment).HasMaxLength(2000);

            entity.HasOne(r => r.Book)
                .WithMany(b => b.Reviews)
                .HasForeignKey(r => r.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(t => t.HasCheckConstraint("ck_reviews_rating_range", "rating between 1 and 5"));
        });

        // Applied last so it renames everything the configuration above set
        // up, including the auto-generated "book_genre" join table.
        modelBuilder.UseSnakeCaseNames();
    }
}
```

**Why apply `UseSnakeCaseNames()` last:** every prior call in `OnModelCreating`
can still add or rename properties, keys, and indexes. Applying the
convention last guarantees it sees the model in its final shape, including
things EF Core generated for you (like the `book_genre` join table) that you
never explicitly configured.

### Step 0.4: Seed Deterministic Data

**Your Task:**
Create `Seed/LibrarySeeder.cs` with a static `SeedAsync(LibraryDbContext)`
method that:
1. Returns immediately if `context.Books` already has any rows (idempotent).
2. Creates a dozen authors, half a dozen publishers, and eight genres.
3. Creates about 90 books, each with a random (but **seeded**, i.e.
   `new Random(20260915)`, not `new Random()`) author, publisher, 1-2
   genres, 1-4 tags from a fixed pool, a `Metadata` JSON string like
   `{"language":"en","pages":320,"featured":false}`, and 0-6 reviews with
   ratings 1-5.

The exact tag pool, title generator, and price/date ranges are up to you —
what matters is that the seed is **deterministic**: run it twice against two
empty databases and you should get byte-identical data both times. That's
what lets later parts (and the tests in `../tests`) assert on concrete
numbers instead of "some number greater than zero". See
`../solution/Seed/LibrarySeeder.cs` for one complete implementation once
you've tried your own.

**Why determinism matters here specifically:** every part after this one
queries this seed data and expects to find real matches — books tagged
`"scifi"`, books with `"featured":true` in their metadata, authors with more
than one book. A non-deterministic seed (or `new Random()` without a fixed
seed) would make some runs pass and others fail for reasons that have
nothing to do with your query.

### Step 0.5: Wire It Up in Program.cs

**Your Task:**
Replace `Program.cs` with something that builds a `LibraryDbContext` from
`appsettings.json`'s `ConnectionStrings:Default`, calls
`EnsureCreatedAsync()` (this project is about querying, not migrations —
`EnsureCreatedAsync` is the right tool here, `dotnet ef migrations` is
`EfCoreMigrations`' job), then calls `LibrarySeeder.SeedAsync(context)`.

```csharp
using EfCoreQuerying.Persistence;
using EfCoreQuerying.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connectionString = configuration.GetConnectionString("Default")
                        ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");

var options = new DbContextOptionsBuilder<LibraryDbContext>().UseNpgsql(connectionString).Options;
await using var context = new LibraryDbContext(options);

await context.Database.EnsureCreatedAsync();
await LibrarySeeder.SeedAsync(context);

Console.WriteLine("Database ready and seeded.");
```

**Run it:**
```bash
# From 10-EntityFrameworkCore/, start Postgres if you haven't already:
docker compose up -d

# Then, from this project's workspace folder:
dotnet run
```

If you get a connection error, Postgres probably isn't running yet — see
`GETTING_STARTED.md` for troubleshooting.

---

## Part 1: Basic LINQ — Filtering, Sorting, Paging

`Where`, `OrderBy`, `Skip`, and `Take` are the four building blocks behind
almost every list page and API endpoint you'll ever write against EF Core.

**Your Task:**
Add a demo (a static method you can call from `Program.cs`, or just
experiment directly) that:
1. Finds books priced under $20, published in the last 10 years, cheapest
   first, limited to 5 results.
2. Pages through the full catalog 10 at a time, sorted by title — fetch
   "page 2" (`Skip(10).Take(10)`).

```csharp
var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10));
var affordable = await context.Books
    .Where(b => b.Price < 20 && b.PublishedOn >= cutoff)
    .OrderBy(b => b.Price)
    .Take(5)
    .Select(b => new { b.Title, b.Price, b.PublishedOn })
    .ToListAsync();

const int pageSize = 10;
const int page = 2;
var pageTwo = await context.Books
    .OrderBy(b => b.Title)
    .ThenBy(b => b.Id) // tie-breaker -- OrderBy alone isn't stable across pages without one
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(b => b.Title)
    .ToListAsync();
```

**Why the `ThenBy(b => b.Id)`:** `OrderBy(b => b.Title)` alone doesn't
guarantee a stable order when titles tie — without a tie-breaker, `Skip`/
`Take` paging can show you the same row twice across two pages, or skip one
entirely, depending on what the database feels like doing that day.

**Questions to think about:**
1. What happens if you call `.Take(5)` *before* `.OrderBy(...)` instead of
   after? (Try it.)
2. Why would `Skip`/`Take` paging (as opposed to "keyset" paging using
   `Where(b => b.Id > lastSeenId)`) become a performance problem on page
   10,000 of a very large table?

---

## Part 2: Projections vs. Tracked Entities

**Your Task:**
Compare three ways of reading the same 5 books:
1. `context.Books.Take(5).ToListAsync()` — full tracked entities.
2. `context.Books.Select(b => new BookSummaryDto(...)).Take(5).ToListAsync()`
   — a projection.
3. `context.Books.AsNoTracking().Take(5).ToListAsync()` — tracked entity
   shape, but not tracked.

After each, print `context.ChangeTracker.Entries().Count()`.

```csharp
public record BookSummaryDto(int Id, string Title, string AuthorName, string PublisherName, decimal Price, DateOnly PublishedOn);
```

```csharp
var tracked = await context.Books.Take(5).ToListAsync();
Console.WriteLine(context.ChangeTracker.Entries().Count()); // 5

context.ChangeTracker.Clear();

var summaries = await context.Books
    .Take(5)
    .Select(b => new BookSummaryDto(b.Id, b.Title, b.Author.Name, b.Publisher.Name, b.Price, b.PublishedOn))
    .ToListAsync();
Console.WriteLine(context.ChangeTracker.Entries().Count()); // 0 -- projections are never tracked

var readOnly = await context.Books.AsNoTracking().Take(5).ToListAsync();
Console.WriteLine(context.ChangeTracker.Entries().Count()); // 0 -- AsNoTracking skips snapshotting
```

**Why it matters for performance:** every tracked entity gets a snapshot so
EF Core can diff it against its current state on `SaveChanges()` — memory and
CPU spent on bookkeeping you'll never use if the query is read-only. For a
list of 5 books this is noise; for a report over 50,000 rows it's the
difference between a query that returns instantly and one that visibly
drags. Default to `AsNoTracking()` (or a projection, which is implicitly
untracked) for anything you're not about to modify through the same context.

**Questions to think about:**
1. If you `Select` into a DTO, do you even need `AsNoTracking()`? Why or why
   not?
2. When would returning a *tracked* entity from a query still be the right
   call?

---

## Part 3: Include/ThenInclude vs. Projecting Related Data

**Your Task:**
Load the first 3 books with their author, publisher, genres, and reviews two
ways: `Include`/`ThenInclude`, and a `Select` projection into a DTO.

```csharp
public record BookDetailDto(
    int Id, string Title, string AuthorName, string PublisherName,
    IReadOnlyList<string> GenreNames, IReadOnlyList<string> ReviewComments, double? AverageRating);
```

```csharp
var withIncludes = await context.Books
    .Include(b => b.Author)
    .Include(b => b.Publisher)
    .Include(b => b.Genres)
    .Include(b => b.Reviews)
    .AsNoTracking()
    .Take(3)
    .ToListAsync();

var projected = await context.Books
    .Take(3)
    .Select(b => new BookDetailDto(
        b.Id, b.Title, b.Author.Name, b.Publisher.Name,
        b.Genres.Select(g => g.Name).ToList(),
        b.Reviews.Select(r => r.Comment ?? "(no comment)").ToList(),
        b.Reviews.Count > 0 ? b.Reviews.Average(r => (double)r.Rating) : (double?)null))
    .ToListAsync();
```

**When projection is strictly better:** any read-only view — a list page, a
detail page, an API response — where you don't need the ability to mutate
the related entities through the same context afterward. The projection
pulls exactly the columns the DTO needs and never builds a tracked graph;
`Include` pulls every column of every related entity whether you use it or
not. `Include` earns its keep specifically when you're about to add, update,
or remove related rows through the same `DbContext` — e.g. loading a `Book`
with its `Reviews` so you can add one more `Review` to the collection and
call `SaveChanges()`.

**Questions to think about:**
1. What SQL would you *expect* `Include(b => b.Genres).Include(b => b.Reviews)`
   to generate, given `Genres` is many-to-many and `Reviews` is one-to-many?
   (Keep this in mind — Part 4 is about exactly this.)
2. Could you write the projection version without any `Include` calls at
   all? You just did — why does `Select` not need them?

---

## Part 4: AsSplitQuery vs. the Default Single Query

**Your Task:**
Run the same query — `Include(b => b.Genres).Include(b => b.Reviews)` over 5
books — with and without `.AsSplitQuery()`, and compare.

```csharp
var singleQuery = await context.Books
    .Include(b => b.Genres)
    .Include(b => b.Reviews)
    .AsNoTracking()
    .Take(5)
    .ToListAsync();

var splitQuery = await context.Books
    .Include(b => b.Genres)
    .Include(b => b.Reviews)
    .AsSplitQuery()
    .AsNoTracking()
    .Take(5)
    .ToListAsync();
```

**The cartesian-explosion problem:** by default, EF Core joins `Books` to
*both* `Genres` (many-to-many) and `Reviews` (one-to-many) in a **single**
SQL query. A book with 2 genres and 3 reviews comes back from that join as
`2 × 3 = 6` duplicated rows, which EF Core then has to de-duplicate on the
client. With more collection `Include`s, or larger collections, the amount
of duplicated data crossing the wire grows multiplicatively — a query whose
row count *looks* small can move far more data than expected.

`AsSplitQuery()` issues one SQL query per collection navigation instead
(one for `Books`+`Author`+`Publisher`, one for the `Genres` join, one for
`Reviews`), avoiding the duplication entirely — at the cost of multiple
round trips instead of one, and losing the guarantee that all the data
reflects one consistent snapshot (a write between the split queries could,
in principle, be visible in one but not another, unless you wrap the whole
thing in an explicit transaction).

**When to split:** more than one collection `Include` on the same root,
where the collections involved are large or numerous enough that the
duplication is actually moving a meaningful amount of redundant data. **When
not to split:** a single collection `Include` (there's no duplication to
avoid), small/bounded collections, or when you specifically need one
consistent snapshot from a single round trip.

**Questions to think about:**
1. Turn on query logging (see `GETTING_STARTED.md`) and count the SQL
   statements each version generates. Does it match the explanation above?
2. If `Reviews` alone (no `Genres`) were the only `Include`, would splitting
   buy you anything?

---

## Part 5: GroupBy and Aggregates

**Your Task:**
Compute the average rating per book, and the book count per genre.

```csharp
public record BookRatingDto(int BookId, string Title, double AverageRating, int ReviewCount);
public record GenreBookCountDto(string GenreName, int BookCount);
```

```csharp
var ratings = await context.Reviews
    .GroupBy(r => r.BookId)
    .Select(g => new BookRatingDto(g.Key, g.First().Book.Title, g.Average(r => (double)r.Rating), g.Count()))
    .OrderByDescending(r => r.AverageRating)
    .Take(5)
    .ToListAsync();

var perGenre = await context.Genres
    .Select(g => new GenreBookCountDto(g.Name, g.Books.Count))
    .OrderByDescending(g => g.BookCount)
    .ToListAsync();
```

**Why:** `GroupBy` composed with `Select` translates to a SQL `GROUP BY`
with `AVG()`/`COUNT()` — the aggregation happens in the database, not by
pulling every review into memory and averaging in C#. The `g.Books.Count`
pattern (counting a navigation collection per outer row) is another
aggregate EF Core can fully translate, this time without an explicit
`GroupBy` at all.

**Questions to think about:**
1. What happens if you write `context.Reviews.GroupBy(r => r.BookId).ToList()`
   and *then* `.Select(...)` in memory, instead of composing `Select` before
   `ToListAsync()`? Which one runs in the database?
2. Books with zero reviews don't appear in the `ratings` result at all — why
   not, and how would you include them with an average of, say, `null`?

---

## Part 6: The Client-vs-Server-Evaluation Gotcha

Some LINQ expressions simply cannot become SQL. A call to an arbitrary C#
method is the textbook case — the database has no idea what that method
does.

**Your Task:**
Write a local method and try to use it inside `Where`:

```csharp
static bool HasRepeatedWord(string title)
{
    var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return words.Length != words.Distinct(StringComparer.OrdinalIgnoreCase).Count();
}

// This throws InvalidOperationException -- EF Core can't translate a call
// to an arbitrary C# method into SQL, and (since EF Core 3.0) refuses to
// silently fall back to pulling the whole table into memory to filter there.
var broken = await context.Books.Where(b => HasRepeatedWord(b.Title)).ToListAsync();
```

Then fix it two ways:

```csharp
// Fix 1: rewrite as something the provider CAN translate.
var count = await context.Books.Where(b => b.Title.Contains("The ")).CountAsync();

// Fix 2: when the logic genuinely can't be expressed in SQL, narrow the
// result set in SQL FIRST, then materialize and apply the client-only
// logic to the much smaller set explicitly. This is legitimate client
// evaluation -- the difference from the "broken" version is that it's
// deliberate, visible, and bounded.
var candidates = await context.Books.Where(b => b.Price < 15).Select(b => b.Title).ToListAsync();
var repeatedWordTitles = candidates.Where(HasRepeatedWord).ToList();
```

**Why it throws instead of silently misbehaving:** older EF (and EF Core
1.x/2.x) used to fall back to client evaluation automatically whenever it
hit something it couldn't translate — which meant a `Where` clause that
*looked* selective could secretly download an entire table and filter it in
memory, with no warning. EF Core 3.0 made that a hard error specifically so
this kind of silent performance cliff can't happen unnoticed.

**Questions to think about:**
1. Before running it, guess: will `context.Books.Where(b => b.Title.ToUpper() == "DUNE")`
   throw, or translate? (Try it — string methods are a good way to build
   intuition for what's translatable.)
2. In Fix 2, what would happen to performance if the `Where(b => b.Price < 15)`
   filter were removed entirely?

---

## Part 7: Raw SQL — FromSqlInterpolated and ExecuteSqlInterpolatedAsync

Some queries are just awkward in LINQ. "The cheapest book per author" is a
top-N-per-group query — expressible in LINQ with a correlated subquery, but
contorted. A window function is the natural tool, and EF Core's LINQ surface
doesn't expose window functions directly.

**Your Task:**

```csharp
var minPrice = 10m;
var cheapestPerAuthor = await context.Books
    .FromSqlInterpolated($"""
        select b.* from (
            select b.*, row_number() over (partition by b.author_id order by b.price asc) as rn
            from books b
        ) b
        where b.rn = 1 and b.price >= {minPrice}
        """)
    .OrderBy(b => b.Price) // further LINQ composes onto the raw SQL like any other IQueryable
    .ToListAsync();
```

Then a bulk update — a 10% discount on every book tagged `"bestseller"`,
applied directly in the database with no entities loaded and no
`SaveChanges()`:

```csharp
var tag = "bestseller";
var rowsAffected = await context.Database.ExecuteSqlInterpolatedAsync($"""
    update books
    set price = round(price * 0.9, 2)
    where {tag} = any(tags)
    """);
```

**Why interpolation (not concatenation) is mandatory:** the `{}`
placeholders above are **not** string concatenation. EF Core turns each one
into a real ADO.NET parameter (`@p0`, `@p1`, ...) before the SQL ever reaches
Postgres — user input can't break out of the query and become part of its
structure. This is true specifically because you used `FromSqlInterpolated`/
`ExecuteSqlInterpolatedAsync` with a C# interpolated string (`$"..."`).

```csharp
// NEVER do this -- FromSqlRaw + concatenation is a SQL injection hole:
context.Books.FromSqlRaw($"select * from books where title = '" + userInput + "'");
// A userInput of  ' OR '1'='1  turns your WHERE clause into "true for every row".
```

`FromSqlInterpolated`/`ExecuteSqlInterpolatedAsync` look almost identical to
the dangerous version — same `$"..."` syntax — which is exactly why it's
worth internalizing the difference: the *method you call* determines whether
those `{}` become safe parameters or get pasted into the SQL text.

**Questions to think about:**
1. Why does `.OrderBy(...)` after `FromSqlInterpolated(...)` work at all —
   what is `FromSqlInterpolated` returning?
2. What would you have to change for `ExecuteSqlInterpolatedAsync`'s update
   to only affect books from one publisher?

---

## Part 8: PostgreSQL-only Querying Features

**Your Task:**
Three PostgreSQL-specific query patterns:

```csharp
// 1. Case-insensitive search: Postgres text comparison is case-sensitive by
//    default (unlike, say, SQL Server's default collation), so plain
//    Contains/StartsWith won't catch a differently-cased match. ILike does.
var ilikeMatches = await context.Books
    .Where(b => EF.Functions.ILike(b.Title, "%the%"))
    .ToListAsync();

// 2. Array column: Tags.Contains(...) translates to Postgres's
//    "value = ANY(tags)" -- an index-friendly membership test against the
//    native text[] column.
var scifiBooks = await context.Books.Where(b => b.Tags.Contains("scifi")).ToListAsync();

// "does this book have ANY of these tags" translates to array overlap (&&).
var wanted = new[] { "gothic", "horror" };
var overlapping = await context.Books.Where(b => b.Tags.Any(t => wanted.Contains(t))).ToListAsync();

// 3. JSONB: EF.Functions.JsonContains translates to the @> containment
//    operator -- "does the metadata document contain this fragment".
var englishBooks = await context.Books
    .Where(b => EF.Functions.JsonContains(b.Metadata, """{"language":"en"}"""))
    .ToListAsync();

// Extracting a single scalar JSON property isn't something LINQ over a
// jsonb-backed string can express -- there's no CLR property to point at.
// That's a job for raw SQL's ->> operator (Part 7's tool, applied here):
var longBooks = await context.Books
    .FromSqlInterpolated($"select * from books where (metadata ->> 'pages')::int > {500}")
    .ToListAsync();
```

**Why these need Postgres specifically:** `ILIKE`, `@>`/`ANY`/`&&` on
arrays, and `jsonb` containment are all Postgres operators with no SQLite or
SQL Server equivalent EF Core could fall back to — this is the one part of
the exercise you genuinely cannot do against an in-memory or SQLite
database, which is why this whole module runs against real Postgres.

**Questions to think about:**
1. `EF.Functions.ILike(b.Title, "%the%")` vs. `b.Title.ToLower().Contains("the")`
   — both give a case-insensitive-ish result. Which one can use an index,
   and why?
2. What SQL operator would you expect `Tags.Contains("scifi")` to become if
   `Tags` were a `List<string>` instead of a `string[]`? (Try it.)

---

## Part 9: Compiled Queries

**Your Task:**
Compile a query for a plausible hot path — looking up a book by ISBN — and
call it many times.

```csharp
public static class CompiledQueries
{
    public static readonly Func<LibraryDbContext, string, Task<Book?>> GetBookByIsbn =
        EF.CompileAsyncQuery((LibraryDbContext context, string isbn) =>
            context.Books.AsNoTracking().FirstOrDefault(b => b.Isbn == isbn));
}

var book = await CompiledQueries.GetBookByIsbn(context, "978-0-100000-0");
```

**Why, and when it's worth it:** EF Core already caches the *translation* of
a query the first time it sees a given expression-tree shape, and reuses
that cached SQL on subsequent calls — for most application code, that
caching is already enough. `EF.CompileAsyncQuery` goes one step further by
binding the delegate once up front, skipping even the cache lookup and
expression-tree walk on every call. That's a small, CPU-side saving that
only matters once a query runs *very* often on a genuinely hot path — a
per-request lookup in a high-throughput API, not an admin report that runs
once a minute. The cost: the query's shape is now frozen in a static
delegate, so it can't be conditionally composed the way an ordinary
LINQ-returning method can (no "add a `Where` only if a filter was
provided").

**Questions to think about:**
1. Why does `GetBookByIsbn` take a `LibraryDbContext` as its first
   parameter, when you never pass a context explicitly when calling
   `context.Books.Where(...)` normally?
2. Would a compiled query make sense for a search endpoint where the caller
   can supply zero or more optional filters? Why or why not?

---

## Reflection Questions

After completing this exercise, answer these:

1. **When is `AsNoTracking()` the wrong choice** — i.e. when do you actually
   want a tracked entity back from a query?
2. **`Include` vs. projection**: name one scenario in this exercise's domain
   (books, authors, reviews) where `Include` is clearly the right tool, and
   one where projection clearly is.
3. **Why does EF Core throw on untranslatable LINQ instead of silently
   falling back to client evaluation?** What would the alternative cost you
   in a codebase you didn't write?
4. **What makes `FromSqlInterpolated` safe against SQL injection** when it
   looks, syntactically, almost identical to the unsafe
   `FromSqlRaw(rawString + userInput)` pattern?
5. **Which of the nine parts in this exercise could you NOT have done
   against SQLite or an in-memory provider, and why?**

---

## Summary

You've learned:
- Filtering, sorting, and paging with `Where`/`OrderBy`/`Skip`/`Take`
- Projections vs. tracked entities, and what `AsNoTracking()` buys you
- `Include`/`ThenInclude` vs. projecting related data, and when each wins
- The multi-collection cartesian-explosion problem and `AsSplitQuery()`
- `GroupBy` and server-side aggregates
- Recognizing and fixing untranslatable LINQ
- Raw SQL with `FromSqlInterpolated`/`ExecuteSqlInterpolatedAsync`, safely
- PostgreSQL-only querying: `ILIKE`, arrays, JSONB
- Compiled queries, and when the added complexity is worth it

## Next Steps

- Compare your work against [`solution/`](solution/)
- Run the test suite: `dotnet test ../tests` (needs Docker for Testcontainers)
- Move on to **EfCoreTransactions** — explicit transactions, optimistic
  concurrency, and isolation levels, all against the same kind of real
  PostgreSQL behavior this exercise relied on
