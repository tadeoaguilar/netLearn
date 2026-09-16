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

        // Two rows both named "Science Fiction" would just be confusing
        // duplicates, so the catalog treats genre names as unique.
        builder.HasIndex(g => g.Name).IsUnique();
    }
}
