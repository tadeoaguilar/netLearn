using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreModeling.Configurations;

/// <summary>
/// Part 1: the first entity configuration. Fluent API, not data annotations
/// -- see the comment on LibraryDbContext for why.
/// </summary>
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
