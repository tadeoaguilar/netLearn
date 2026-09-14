using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VerticalSlice.Api.Common.Domain;

namespace VerticalSlice.Api.Common.Database;

/// <summary>
/// Genuinely shared: every slice reads and writes the same tables, so the
/// context belongs in Common/. The test for "does this belong in Common?" is
/// whether two unrelated features would both change it -- not whether two
/// features happen to use it today.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    // SQLite cannot ORDER BY a DateTimeOffset; store UTC ticks instead.
    private static readonly ValueConverter<DateTimeOffset, long> ToTicks =
        new(v => v.UtcTicks, t => new DateTimeOffset(t, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> ToNullableTicks =
        new(v => v == null ? null : v.Value.UtcTicks,
            t => t == null ? null : new DateTimeOffset(t.Value, TimeSpan.Zero));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(project =>
        {
            project.HasKey(p => p.Id);
            project.Property(p => p.Id).ValueGeneratedNever();
            project.Property(p => p.Name).IsRequired().HasMaxLength(200);
            project.Property(p => p.CreatedAt).HasConversion(ToTicks);

            project.HasMany(p => p.Tasks)
                .WithOne()
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskItem>(task =>
        {
            task.HasKey(t => t.Id);
            task.Property(t => t.Id).ValueGeneratedNever();
            task.Property(t => t.Title).IsRequired().HasMaxLength(200);
            task.Property(t => t.Description).HasMaxLength(2000);
            task.Property(t => t.Assignee).HasMaxLength(200);
            task.Property(t => t.State).HasConversion<string>().HasMaxLength(20);

            // Ordinal, not text -- Priority is ordered, and text sorts
            // lexicographically ("High" below "Low"). Same trap as module 03.
            task.Property(t => t.Priority).HasConversion<int>();

            task.Property(t => t.CreatedAt).HasConversion(ToTicks);
            task.Property(t => t.CompletedAt).HasConversion(ToNullableTicks);

            task.HasIndex(t => t.ProjectId);
            task.HasIndex(t => t.State);
        });
    }
}
