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
        builder.ToTable("books", t => t.HasCheckConstraint(
            "ck_books_rating_range",
            "rating IS NULL OR (rating BETWEEN 1 AND 5)"));
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title).IsRequired().HasMaxLength(300);

        builder.HasOne(b => b.Author)
            .WithMany(a => a.Books)
            .HasForeignKey(b => b.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Books are commonly looked up / sorted by title; this index is the
        // difference between a sequential scan and an index scan once the
        // table has more than a handful of rows.
        builder.HasIndex(b => b.Title);

        builder.HasOne(b => b.Genre)
            .WithMany()
            .HasForeignKey(b => b.GenreId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>
/// A small, fixed reference table -- the kind of data that belongs in
/// <c>HasData</c> rather than a runtime seed. See EXERCISE.md Part 5 for the
/// contrast with the idempotent runtime seed in <see cref="EfCoreMigrations.Data.SeedData"/>.
/// </summary>
public class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.ToTable("genres");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).IsRequired().HasMaxLength(100);

        // HasData is declarative and versioned: every future `migrations add`
        // diffs this list against what the previous migration inserted and
        // scaffolds the INSERT/UPDATE/DELETE operations for you. That only
        // works for data this stable -- explicit, hand-picked IDs, because EF
        // needs a stable key to diff against across migrations.
        builder.HasData(
            new Genre { Id = 1, Name = "Fiction" },
            new Genre { Id = 2, Name = "Science Fiction" },
            new Genre { Id = 3, Name = "Fantasy" },
            new Genre { Id = 4, Name = "Non-Fiction" },
            new Genre { Id = 5, Name = "Biography" },
            new Genre { Id = 6, Name = "Mystery" });
    }
}
