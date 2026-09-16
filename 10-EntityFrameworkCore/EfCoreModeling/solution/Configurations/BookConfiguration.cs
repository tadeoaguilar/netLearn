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

        // --- Part 4: value converter ---
        // Isbn is a validated wrapper, not a primitive EF understands
        // natively. HasConversion tells EF how to turn one into a string for
        // storage, and how to turn a stored string back into an Isbn (which
        // re-validates it) when reading a row back.
        //
        // Trade-off vs. just mapping a plain `string Isbn`: every Book now
        // carries a guaranteed-valid ISBN wherever it's used in C#, at the
        // cost of a converter running on every read and write, and of the
        // property no longer being directly usable in most raw-SQL/LINQ
        // string operations (EF.Functions.Like, string.Contains) without an
        // explicit ".Value" or a second conversion.
        builder.Property(b => b.Isbn)
            .HasConversion(isbn => isbn.Value, value => new Isbn(value))
            .HasMaxLength(13)
            .IsRequired();

        // --- Part 7: unique index ---
        builder.HasIndex(b => b.Isbn).IsUnique();

        // --- Part 3: owned type ---
        // Money has no identity or table of its own -- OwnsOne maps its two
        // properties as extra columns inline on "books" (amount, currency
        // once SnakeCaseNaming runs). It can never be queried independently
        // of the Book that owns it.
        builder.OwnsOne(b => b.Price, price =>
        {
            price.Property(p => p.Amount).HasPrecision(10, 2).IsRequired();
            price.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        });

        // --- Part 2: one-to-many ---
        builder.HasOne(b => b.Publisher)
            .WithMany(p => p.Books)
            .HasForeignKey(b => b.PublisherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Author)
            .WithMany(a => a.Books)
            .HasForeignKey(b => b.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Part 6: table-per-hierarchy ---
        // Every DigitalBook row lives in "books" too, distinguished by this
        // discriminator column. Querying context.Set<Book>() returns every
        // row (base and derived); context.Set<DigitalBook>() filters to rows
        // discriminated as "digital_book".
        builder.HasDiscriminator<string>("BookType")
            .HasValue<Book>("book")
            .HasValue<DigitalBook>("digital_book");

        builder.Property<string>("BookType").HasMaxLength(20).IsRequired();
    }
}
