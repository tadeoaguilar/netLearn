# Exercise: Modeling a Library Catalog with EF Core

## Overview
In this exercise you'll build the data model for a small library catalog --
authors, publishers, genres, books, and reviews -- using EF Core's Fluent
API against PostgreSQL. You'll work through eight parts, each adding one
mapping technique: a first entity, one-to-many relationships, an owned
value object, a value converter, an explicit many-to-many with a payload
column, table-per-hierarchy inheritance, indexes and constraints, and
finally a snake_case naming convention for the whole schema.

Nothing in this exercise needs Postgres to actually be running. EF Core
builds its model the first time something touches `context.Model`, and
that never opens a connection -- `UseNpgsql(...)` just records a connection
string. You can build the model, inspect it, and run every test in
`tests/` with no database at all. (You'll still want Postgres running --
via `docker compose up -d` in `10-EntityFrameworkCore/` -- for later
modules in this repository, which do query and migrate a real database.)

## Learning Goals
By completing this exercise, you will:
- Configure entities with `IEntityTypeConfiguration<T>` and the Fluent API,
  and explain why that beats data annotations for this kind of model
- Map one-to-many relationships explicitly, including the foreign key and
  delete behavior
- Model a value object as an EF Core **owned type**
- Map a validated wrapper type to a primitive column with a **value
  converter**
- Model a many-to-many relationship **with a payload column** using an
  explicit join entity
- Map an inheritance hierarchy with **table-per-hierarchy** (TPH)
- Add a unique index, a check constraint, and required/max-length strings
- Apply a naming convention across an entire model in one place

---

## The Scenario

You're modeling a small library catalog:
- **Author** writes many **Book**s
- **Publisher** publishes many **Book**s
- A **Book** has a **Price** (a value object: amount + currency) and an
  **Isbn** (a validated string, not a bare primitive)
- A **Book** can be tagged with many **Genre**s, and a **Genre** applies to
  many **Book**s -- with an `AddedAt` timestamp on the tag itself
- A **Book** can be a **DigitalBook** (adds file size and format), stored
  in the same table as every other book
- A **Review** belongs to one **Book**, and its `Rating` must be 1-5

---

## Part 1: The DbContext and Your First Entity

### Why Fluent API, Not Data Annotations?

You could configure `Author` with attributes:

```csharp
public class Author
{
    [Required, MaxLength(200)]
    public string Name { get; set; }
}
```

This exercise uses the Fluent API (`IEntityTypeConfiguration<T>`) instead,
for one reason: **attributes tie the entity class to EF Core**. The moment
`Author` references `System.ComponentModel.DataAnnotations`, it's no
longer a plain object that happens to get persisted -- it's a class that
*knows* it's persisted. Fluent API keeps that knowledge in a separate
Persistence/Configurations layer, so every entity stays a plain,
framework-free POCO: easy to unit test, easy to serialize, easy to reuse
if the storage technology ever changes.

### Step 1.1: The First Entity

**Your Task:**
Create `Entities/Author.cs`:

```csharp
namespace EfCoreModeling.Entities;

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }

    public ICollection<Book> Books { get; set; } = new List<Book>();
}
```

Notice there's nothing here that mentions EF Core at all -- not even a
`using`. `Book` doesn't exist yet; you'll add it in Part 2, and this file
won't need to change again.

### Step 1.2: Configure It with Fluent API

**Your Task:**
Create `Configurations/AuthorConfiguration.cs`:

```csharp
using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreModeling.Configurations;

public class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable("authors");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Bio).HasMaxLength(4000);
    }
}
```

### Step 1.3: The DbContext

**Your Task:**
Create `Persistence/LibraryDbContext.cs`:

```csharp
using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCoreModeling.Persistence;

public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<Author> Authors => Set<Author>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraryDbContext).Assembly);
    }
}
```

`ApplyConfigurationsFromAssembly` finds every `IEntityTypeConfiguration<T>`
in the assembly and applies it, so you never have to remember to register a
new configuration by hand -- create the file, and it's picked up.

### Step 1.4: Wire It Up and Run

**Your Task:**
Replace `Program.cs`:

```csharp
using EfCoreModeling.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Library")));

var host = builder.Build();

using var scope = host.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

foreach (var entityType in context.Model.GetEntityTypes())
{
    Console.WriteLine($"{entityType.ClrType.Name} -> table \"{entityType.GetTableName()}\"");
    foreach (var property in entityType.GetProperties())
    {
        Console.WriteLine($"    {property.Name} -> {property.GetColumnName()}");
    }
}
```

**Run it:**
```bash
dotnet run
```

You should see `Author` and its three columns printed -- `Id`, `Name`,
`Bio` -- with no database connection involved. Accessing `context.Model`
is what triggers EF Core to build the model from your configuration; it
never touches the network.

**Questions to think about:**
1. What would break if `AuthorConfiguration` lived inside `Author.cs`
   itself, as a static nested class? (Nothing would *break* -- but what
   would you lose?)
2. `ApplyConfigurationsFromAssembly` scans the whole assembly. What's the
   risk of that, and how would you scope it down in a larger project?

---

## Part 2: One-to-Many Relationships

Every `Book` belongs to exactly one `Publisher` and one `Author`; each of
those has many `Book`s.

### Step 2.1: Publisher

**Your Task:**
Create `Entities/Publisher.cs`:

```csharp
namespace EfCoreModeling.Entities;

public class Publisher
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Book> Books { get; set; } = new List<Book>();
}
```

And `Configurations/PublisherConfiguration.cs`:

```csharp
using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreModeling.Configurations;

public class PublisherConfiguration : IEntityTypeConfiguration<Publisher>
{
    public void Configure(EntityTypeBuilder<Publisher> builder)
    {
        builder.ToTable("publishers");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
    }
}
```

### Step 2.2: Book, with Both Foreign Keys

**Your Task:**
Create `Entities/Book.cs`:

```csharp
namespace EfCoreModeling.Entities;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly PublishedOn { get; set; }

    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;

    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;
}
```

You'll come back to this file three more times -- Parts 3, 4 and 5 each
add one more member to it. That's normal: a real entity accumulates
mapped members as the model grows, and each addition is small.

**Your Task:**
Create `Configurations/BookConfiguration.cs`:

```csharp
using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreModeling.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title).IsRequired().HasMaxLength(300);
        builder.Property(b => b.PublishedOn).IsRequired();

        builder.HasOne(b => b.Publisher)
            .WithMany(p => p.Books)
            .HasForeignKey(b => b.PublisherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Author)
            .WithMany(a => a.Books)
            .HasForeignKey(b => b.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

`OnDelete(DeleteBehavior.Restrict)` means the database refuses to delete
an `Author` or `Publisher` that still has books -- the opposite choice
from `Review`'s relationship to `Book` in Part 7, where cascading makes
more sense. Deleting a book should take its reviews with it; deleting an
author should not silently delete every book they wrote.

### Step 2.3: Update the DbContext

**Your Task:**
Update `Persistence/LibraryDbContext.cs` to add the two new `DbSet`s:

```csharp
public DbSet<Author> Authors => Set<Author>();
public DbSet<Publisher> Publishers => Set<Publisher>();
public DbSet<Book> Books => Set<Book>();
```

**Run it again** (`dotnet run`) and you should see `Book`,
`Publisher`, and their columns and foreign keys.

**Questions to think about:**
1. What SQL error would you get, in practice, if you tried to delete a
   `Publisher` row that still has books, given `DeleteBehavior.Restrict`?
2. `HasForeignKey(b => b.PublisherId)` points at an `int`, not the
   `Publisher` navigation. Why does the foreign key property need to exist
   on `Book` at all, instead of relying purely on the `Publisher`
   navigation?

---

## Part 3: An Owned Type for Price

`Book.Price` isn't a single primitive -- it's an amount and a currency
that travel together and are never independently meaningful. That's
exactly what EF Core's **owned types** are for.

### Step 3.1: The Value Object

**Your Task:**
Create `Entities/Money.cs`:

```csharp
namespace EfCoreModeling.Entities;

/// <summary>
/// A value object with no identity of its own -- two Money instances with
/// the same Amount and Currency are interchangeable, and neither is ever
/// looked up by an Id.
/// </summary>
public record Money(decimal Amount, string Currency);
```

### Step 3.2: Add It to Book

**Your Task:**
Update `Entities/Book.cs`, adding one property:

```csharp
namespace EfCoreModeling.Entities;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly PublishedOn { get; set; }
    public Money Price { get; set; } = new(0m, "USD");

    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;

    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;
}
```

### Step 3.3: Configure It with OwnsOne

**Your Task:**
Update `Configurations/BookConfiguration.cs`, adding this inside
`Configure`:

```csharp
        // Money has no identity or table of its own -- OwnsOne maps its
        // two properties as extra columns inline on "books". It can never
        // be queried independently of the Book that owns it.
        builder.OwnsOne(b => b.Price, price =>
        {
            price.Property(p => p.Amount).HasPrecision(10, 2).IsRequired();
            price.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        });
```

**Run it again.** You'll see `Book` now carries an owned navigation to
`Price`, with `Amount` and `Currency` columns living on the same `books`
table -- there is no `prices` table.

**Questions to think about:**
1. What would change if you called `.ToTable("book_prices")` inside the
   `OwnsOne` configuration instead of leaving it to share `books`?
2. `Money` is a `record`, which gives it structural equality for free. Why
   does that matter for a value object specifically, more than it would
   for `Author` or `Book`?

---

## Part 4: A Value Converter for Isbn

An ISBN isn't just any string -- it's either a valid ISBN-10 or ISBN-13,
or it's a bug. `Book.Isbn` is going to be a small wrapper type that makes
"not a valid ISBN" impossible to represent once it's constructed. EF Core
has no idea how to store that type natively, though -- it only speaks
primitives. A **value converter** bridges the gap.

### Step 4.1: The Wrapper Type

**Your Task:**
Create `Entities/Isbn.cs`:

```csharp
namespace EfCoreModeling.Entities;

public readonly struct Isbn : IEquatable<Isbn>
{
    public string Value { get; }

    public Isbn(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("An ISBN cannot be empty.", nameof(value));

        var normalized = value.Replace("-", "").Replace(" ", "");

        if (normalized.Length is not (10 or 13) || !normalized.All(char.IsLetterOrDigit))
            throw new ArgumentException($"'{value}' is not a valid ISBN-10/13.", nameof(value));

        Value = normalized;
    }

    public override string ToString() => Value;

    public bool Equals(Isbn other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Isbn other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(Isbn left, Isbn right) => left.Equals(right);
    public static bool operator !=(Isbn left, Isbn right) => !left.Equals(right);
}
```

### Step 4.2: Add It to Book

**Your Task:**
Update `Entities/Book.cs`, adding one more property:

```csharp
    public Isbn Isbn { get; set; }
```

(Add it anywhere among the scalar properties -- next to `Title` is a
natural place.)

### Step 4.3: Configure the Conversion

**Your Task:**
Update `Configurations/BookConfiguration.cs`, adding this inside
`Configure`:

```csharp
        // Isbn is a validated wrapper, not a primitive EF understands
        // natively. HasConversion tells EF how to turn one into a string
        // for storage, and how to turn a stored string back into an Isbn
        // (re-validating it) when reading a row back.
        builder.Property(b => b.Isbn)
            .HasConversion(isbn => isbn.Value, value => new Isbn(value))
            .HasMaxLength(13)
            .IsRequired();
```

**The trade-off, compared to just mapping a plain `string Isbn`:** every
`Book` now carries a guaranteed-valid ISBN wherever it's used in C#, at
the cost of a converter running on every read and write, and of the
property no longer being directly usable in most raw-SQL or LINQ string
operations (`EF.Functions.Like`, `.Contains(...)`) without unwrapping
`.Value` first. For a field this rarely searched by substring, that trade
is worth it; for something like a free-text `Title`, it probably wouldn't
be.

**Run it again.** You'll see the `Isbn` property mapped with `ClrType`
`Isbn` but a provider type of `string` underneath.

**Questions to think about:**
1. Why does the converter's "read" direction (`value => new Isbn(value)`)
   re-run the same validation as the constructor, instead of trusting
   that anything already in the database must be valid?
2. Would an enum converted with `HasConversion<string>()` (storing
   `"Hardcover"` instead of `0`) solve a similar problem to this one? What
   would you gain and lose compared to the default int mapping?

---

## Part 5: Many-to-Many with a Payload

A `Book` can have many `Genre`s, and a `Genre` applies to many `Book`s --
a classic many-to-many. EF Core 5+ can infer an **implicit** join table
for a plain many-to-many with one line
(`HasMany(b => b.Genres).WithMany(g => g.Books)`), and hide that join
table from you entirely.

That shortcut has no room for extra columns, though. The moment you need
to know *when* a genre was added to a book, you need the join table to be
a first-class entity you can add a property to -- which means configuring
it **explicitly**, not through the implicit skip-navigation shortcut.

### Step 5.1: Genre

**Your Task:**
Create `Entities/Genre.cs`:

```csharp
namespace EfCoreModeling.Entities;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // No direct ICollection<Book> here -- the relationship to Book always
    // goes through BookGenre below.
    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
}
```

And `Configurations/GenreConfiguration.cs`:

```csharp
using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreModeling.Configurations;

public class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.ToTable("genres");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(g => g.Name).IsUnique();
    }
}
```

### Step 5.2: The Join Entity, with Its Payload

**Your Task:**
Create `Entities/BookGenre.cs`:

```csharp
namespace EfCoreModeling.Entities;

public class BookGenre
{
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;

    public DateTime AddedAt { get; set; }
}
```

`AddedAt` is the payload -- the whole reason this is an explicit entity.

**Your Task:**
Create `Configurations/BookGenreConfiguration.cs`:

```csharp
using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreModeling.Configurations;

public class BookGenreConfiguration : IEntityTypeConfiguration<BookGenre>
{
    public void Configure(EntityTypeBuilder<BookGenre> builder)
    {
        builder.ToTable("book_genres");

        // Composite primary key: a book can only be tagged with a given
        // genre once, which a plain surrogate Id would not enforce.
        builder.HasKey(bg => new { bg.BookId, bg.GenreId });

        builder.Property(bg => bg.AddedAt).IsRequired();

        builder.HasOne(bg => bg.Book)
            .WithMany(b => b.BookGenres)
            .HasForeignKey(bg => bg.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bg => bg.Genre)
            .WithMany(g => g.BookGenres)
            .HasForeignKey(bg => bg.GenreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Step 5.3: Give Book the Other Side

**Your Task:**
Update `Entities/Book.cs`, adding one property:

```csharp
    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
```

### Step 5.4: Update the DbContext

**Your Task:**
Update `Persistence/LibraryDbContext.cs`, adding:

```csharp
public DbSet<Genre> Genres => Set<Genre>();
public DbSet<BookGenre> BookGenres => Set<BookGenre>();
```

**Questions to think about:**
1. If you later needed `book.Genres` to list genre names directly (no
   `.Select(bg => bg.Genre.Name)`), could you add that as a convenience
   property without giving up the explicit join entity? How?
2. Composite keys mean no single `GenreId` column can repeat with a given
   `BookId`. What real bug does that prevent, that a plain non-unique
   index on `(BookId, GenreId)` would not?

---

## Part 6: Table-Per-Hierarchy Inheritance

A `DigitalBook` is still a `Book` -- it has a title, an author, a price,
reviews, genres -- but it also has a file size and a format. Rather than a
separate `digital_books` table, **table-per-hierarchy** (TPH) stores every
`DigitalBook` row in the same `books` table as ordinary books,
distinguished by a **discriminator column**.

### Step 6.1: The Subtype

**Your Task:**
Create `Entities/DigitalBook.cs`:

```csharp
namespace EfCoreModeling.Entities;

public class DigitalBook : Book
{
    public double FileSizeMb { get; set; }
    public string Format { get; set; } = string.Empty;
}
```

### Step 6.2: Configure the Discriminator

**Your Task:**
Update `Configurations/BookConfiguration.cs`, adding this inside
`Configure`:

```csharp
        // Every DigitalBook row lives in "books" too, distinguished by
        // this discriminator column. Querying context.Set<Book>() returns
        // every row (base and derived); context.Set<DigitalBook>()
        // filters to rows discriminated as "digital_book".
        builder.HasDiscriminator<string>("BookType")
            .HasValue<Book>("book")
            .HasValue<DigitalBook>("digital_book");

        builder.Property<string>("BookType").HasMaxLength(20).IsRequired();
```

### Step 6.3: Configure the Subtype's Own Properties

**Your Task:**
Create `Configurations/DigitalBookConfiguration.cs`:

```csharp
using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreModeling.Configurations;

public class DigitalBookConfiguration : IEntityTypeConfiguration<DigitalBook>
{
    public void Configure(EntityTypeBuilder<DigitalBook> builder)
    {
        builder.Property(d => d.FileSizeMb).HasPrecision(8, 2);
        builder.Property(d => d.Format).HasMaxLength(20);
    }
}
```

EF Core is fine with a second `IEntityTypeConfiguration` targeting a TPH
subtype -- it merges into the same `books` table as `BookConfiguration`.

### Step 6.4: Update the DbContext

**Your Task:**
Update `Persistence/LibraryDbContext.cs`, adding:

```csharp
public DbSet<DigitalBook> DigitalBooks => Set<DigitalBook>();
```

**Run it again.** Look closely at `FileSizeMb` and `Format` in the printed
model: they're columns on `books`, the same table `Book` maps to.

**Questions to think about:**
1. Every plain `Book` row now has `NULL` in its `file_size_mb` and
   `format` columns. Is that a problem? What would table-per-type (TPT) --
   a separate `digital_books` table holding only the extra columns -- cost
   you instead?
2. If you queried `context.Set<Book>().ToList()`, would `DigitalBook` rows
   be included? What about `context.Set<DigitalBook>().ToList()` --  would
   it ever return a plain `Book`?

---

## Part 7: Indexes and Constraints

Two more constraints tie the model together: `Book.Isbn` must be unique
(you already added that index in Part 4), and `Review.Rating` must be
between 1 and 5 -- enforced by the database, not just by C#.

### Step 7.1: The Review Entity

**Your Task:**
Create `Entities/Review.cs`:

```csharp
namespace EfCoreModeling.Entities;

public class Review
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
}
```

### Step 7.2: Give Book the Inverse Navigation

**Your Task:**
Update `Entities/Book.cs`, adding the last property:

```csharp
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
```

### Step 7.3: Configure the Check Constraint

**Your Task:**
Create `Configurations/ReviewConfiguration.cs`:

```csharp
using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreModeling.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        // The constraint SQL references the FINAL column name ("rating"),
        // because the snake_case convention you'll add in Part 8 runs
        // after every IEntityTypeConfiguration and only renames
        // properties/keys/foreign keys/indexes -- it does not (and cannot,
        // safely) rewrite arbitrary SQL text inside a check constraint, so
        // that text has to be written in its snake_case form up front.
        builder.ToTable("reviews", tb =>
            tb.HasCheckConstraint("ck_reviews_rating_range", "rating >= 1 AND rating <= 5"));

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Rating).IsRequired();
        builder.Property(r => r.ReviewerName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Comment).HasMaxLength(2000);

        builder.HasOne(r => r.Book)
            .WithMany(b => b.Reviews)
            .HasForeignKey(r => r.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.BookId);
    }
}
```

### Step 7.4: Update the DbContext

**Your Task:**
Update `Persistence/LibraryDbContext.cs`, adding:

```csharp
public DbSet<Review> Reviews => Set<Review>();
```

At this point your model has every entity and every relationship. Go back
and confirm `Configurations/BookConfiguration.cs` has the unique index
from Part 4:

```csharp
builder.HasIndex(b => b.Isbn).IsUnique();
```

**Questions to think about:**
1. Why does the check constraint belong in the database at all, if the
   application never lets a user submit a rating outside 1-5? What kind
   of bug does it catch that application-level validation doesn't?
2. `HasIndex(r => r.BookId)` (non-unique) speeds up "all reviews for this
   book" queries. What would change in behavior, not just performance, if
   you made it unique instead?

---

## Part 8: Snake-Case Naming, Applied Last

PostgreSQL folds unquoted identifiers to lower case, so a column mapped as
`OwnerId` can only ever be referenced as `"OwnerId"` -- quoted, every
time. `select owner_id from ...` works; `select ownerid from ...` doesn't.
snake_case is the Postgres convention for exactly this reason, and this
whole module follows it.

Rather than calling `.HasColumnName("author_id")` on every single
property, this part applies a **convention**: one pass over the finished
model that renames every column, key, foreign key, and index.

### Step 8.1: The Convention

**Your Task:**
Create `Persistence/SnakeCaseNaming.cs`:

```csharp
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace EfCoreModeling.Persistence;

public static class SnakeCaseNaming
{
    public static void UseSnakeCaseNames(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // An owned type sharing its owner's table (Book.Price, from
            // Part 3) has a shadow property that is BOTH its primary key
            // and the foreign key back to the owner -- it always holds
            // the exact same value as the owner's own primary key, and is
            // never materialized as its own column. EF Core's model
            // validator specifically trusts that link as long as its
            // column name is left at its convention-assigned default;
            // explicitly renaming it makes the validator treat it as an
            // independent column and reject it as incompatible with the
            // owner's primary key. So it's the one property left alone.
            var ownedPrimaryKeyProperties = entity.IsOwned()
                ? entity.FindPrimaryKey()?.Properties.ToHashSet() ?? []
                : [];

            foreach (var property in entity.GetProperties())
            {
                if (ownedPrimaryKeyProperties.Contains(property))
                {
                    continue;
                }

                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName()));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()));
            }
        }
    }

    internal static string? ToSnakeCase(string? name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (current == '_')
            {
                builder.Append('_');
                continue;
            }

            if (char.IsUpper(current) && builder.Length > 0 && builder[^1] != '_')
            {
                var previous = name[i - 1];
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (!char.IsUpper(previous) || nextIsLower)
                {
                    builder.Append('_');
                }
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
```

### Step 8.2: Apply It Last

**Your Task:**
Update `Persistence/LibraryDbContext.cs`'s `OnModelCreating`:

```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraryDbContext).Assembly);

        // Applied LAST, so it renames whatever the configurations above
        // produced -- columns, keys, foreign keys, indexes.
        modelBuilder.UseSnakeCaseNames();
    }
```

Order matters here. If you called `UseSnakeCaseNames()` *before*
`ApplyConfigurationsFromAssembly`, it would rename an empty model and do
nothing.

### Step 8.3: Inspect the Result

**Run it one last time:**
```bash
dotnet run
```

Every column, key, foreign key and index name printed should now be
`snake_case`: `author_id`, `pk_books`, `ix_books_author_id`,
`fk_reviews_books_book_id`, and so on.

**Questions to think about:**
1. Why does `SnakeCaseNaming` leave table names alone entirely (they're
   set with `ToTable("...")` directly in each configuration, already
   lower-case)? What would happen if a configuration forgot to lower-case
   a table name -- would this convention catch it?
2. `ToSnakeCase` treats an existing `_` as already-separated (so it
   doesn't double up on names EF Core already generated, like
   `PK_projects`). Trace through `ToSnakeCase("IX_books_AuthorId")` by
   hand and confirm you get `ix_books_author_id`.

---

## Checking Your Work

The reference solution in `../solution/` implements everything above.
`../tests/` has 46 tests that inspect `context.Model` directly --
no database, no migrations, nothing to start first.

```bash
dotnet test ../tests
```

They point at `solution/` by default. To check **your** work instead,
edit the `ProjectReference` in `tests/EfCoreModeling.Tests.csproj`:

```xml
<ProjectReference Include="../EfCoreModeling/EfCoreModeling.csproj" />
```

Run the tests again. Each failure names the exact mapping it expected --
a foreign key, an index, a column name -- so the failures double as a
checklist for what's left.

---

## Reflection Questions

1. **Fluent API vs. data annotations**: name one thing you can express
   with the Fluent API that a `[Required]`/`[MaxLength]` attribute
   fundamentally cannot.
2. **Owned type vs. a plain reference to another entity**: what's the
   real difference between `Book.Price` (owned) and `Book.Publisher` (a
   normal relationship)? What would change if `Money` had its own `Id`
   and its own `DbSet<Money>`?
3. **Value converters**: besides `Isbn`, name two other properties in a
   typical application that would benefit from a converter instead of a
   bare primitive column.
4. **Explicit join entity vs. implicit skip navigation**: if `BookGenre`
   had no `AddedAt` and never would, would you still choose the explicit
   join entity? What would you gain or lose either way?
5. **TPH vs. TPT**: this exercise chose table-per-hierarchy for
   `DigitalBook`. At what point -- how many subtypes, how many
   subtype-specific columns -- would you reconsider and switch to
   table-per-type?
6. **Naming as a convention**: `SnakeCaseNaming` runs once, over the whole
   model, instead of every configuration calling `.HasColumnName(...)`
   by hand. What's the equivalent trade-off you've seen (or can imagine)
   in a different context -- a linter rule, a code formatter, a build
   convention -- where automating consistency beats relying on everyone
   remembering?

---

## Summary

You've learned:
- Configuring entities with `IEntityTypeConfiguration<T>` and why Fluent
  API keeps entities persistence-ignorant
- One-to-many relationships, explicit foreign keys, and delete behavior
- Modeling a value object as an EF Core owned type
- Mapping a validated wrapper type with a value converter
- Explicit many-to-many with a payload column on the join entity
- Table-per-hierarchy inheritance with a discriminator column
- Unique indexes and check constraints enforced by the database
- Applying a naming convention across an entire model in one place

## Next Steps

Continue to **[EfCoreMigrations](../EfCoreMigrations/)** to see this kind
of model evolve over time -- creating, applying, and hand-editing
migrations instead of building the whole schema in one pass.
