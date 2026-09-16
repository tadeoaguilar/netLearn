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
