using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// SQLite has no native DateTimeOffset and refuses to ORDER BY one. Storing UTC
/// ticks keeps ordering correct and numeric on both providers.
/// </summary>
internal static class Converters
{
    public static readonly ValueConverter<DateTimeOffset, long> Ticks =
        new(v => v.UtcTicks, t => new DateTimeOffset(t, TimeSpan.Zero));

    public static readonly ValueConverter<DateTimeOffset?, long?> NullableTicks =
        new(v => v == null ? null : v.Value.UtcTicks,
            t => t == null ? null : new DateTimeOffset(t.Value, TimeSpan.Zero));
}

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(p => p.Id);

        // Identity is assigned by the DOMAIN. Without this EF assumes a set key
        // means an existing row and issues an UPDATE that matches nothing.
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.OwnerId).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(p => p.CreatedAt).HasConversion(Converters.Ticks);
        builder.Property(p => p.LastModifiedAt).HasConversion(Converters.NullableTicks);
        builder.Property(p => p.CreatedBy).HasMaxLength(200);
        builder.Property(p => p.LastModifiedBy).HasMaxLength(200);

        // Mapped to the backing field: the entity exposes IReadOnlyCollection,
        // and adding a public setter to satisfy EF would undo the encapsulation.
        builder.HasMany(p => p.Tasks)
            .WithOne()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.OpenTaskCount);   // computed, never stored

        builder.HasIndex(p => p.OwnerId);
        builder.HasIndex(p => p.Status);
    }
}

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.AssigneeId).HasMaxLength(200);

        // State is only compared for equality, so text keeps rows readable.
        builder.Property(t => t.State).HasConversion<string>().HasMaxLength(20);

        // Priority is ORDERED -- "order by priority desc" and the
        // MinimumPriority filter both use it. Stored as text, SQL would compare
        // lexicographically and put "High" below "Low".
        builder.Property(t => t.Priority).HasConversion<int>();

        builder.Property(t => t.DueDate).HasConversion(Converters.NullableTicks);
        builder.Property(t => t.CompletedAt).HasConversion(Converters.NullableTicks);
        builder.Property(t => t.CreatedAt).HasConversion(Converters.Ticks);
        builder.Property(t => t.LastModifiedAt).HasConversion(Converters.NullableTicks);
        builder.Property(t => t.CreatedBy).HasMaxLength(200);
        builder.Property(t => t.LastModifiedBy).HasMaxLength(200);

        builder.Ignore(t => t.DomainEvents);

        builder.HasIndex(t => t.ProjectId);
        builder.HasIndex(t => t.State);
        builder.HasIndex(t => t.AssigneeId);
    }
}
