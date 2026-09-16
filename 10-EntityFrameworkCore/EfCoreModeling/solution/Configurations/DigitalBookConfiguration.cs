using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreModeling.Configurations;

/// <summary>
/// Part 6: configuration for the properties that only exist on the derived
/// type. EF Core is fine with a second IEntityTypeConfiguration targeting a
/// TPH subtype -- it merges into the same "books" table as BookConfiguration.
/// </summary>
public class DigitalBookConfiguration : IEntityTypeConfiguration<DigitalBook>
{
    public void Configure(EntityTypeBuilder<DigitalBook> builder)
    {
        builder.Property(d => d.FileSizeMb).HasPrecision(8, 2);
        builder.Property(d => d.Format).HasMaxLength(20);
    }
}
