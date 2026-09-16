using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreModeling.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        // --- Part 7: check constraint ---
        // The constraint SQL references the FINAL column name ("rating"),
        // because SnakeCaseNaming runs after every IEntityTypeConfiguration
        // and only renames properties/keys/foreign keys/indexes -- it does
        // not (and cannot, safely) rewrite arbitrary SQL text inside a check
        // constraint, so that text has to be written in its snake_case form
        // up front.
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
