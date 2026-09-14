using CleanArchitecture.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CleanArchitecture.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    /// <summary>
    /// SQLite has no native DateTimeOffset, and refuses to ORDER BY one:
    /// "SQLite does not support expressions of type 'DateTimeOffset' in ORDER
    /// BY clauses". Storing UTC ticks as an integer keeps ordering correct and
    /// numeric.
    ///
    /// This is a genuine leak of the provider into the mapping layer -- and
    /// exactly why it belongs HERE and not in the domain. TaskItem.CreatedAt
    /// stays a DateTimeOffset; only Infrastructure knows about the workaround.
    /// </summary>
    private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetToTicks =
        new(value => value.UtcTicks,
            ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetToTicks =
        new(value => value == null ? null : value.Value.UtcTicks,
            ticks => ticks == null ? null : new DateTimeOffset(ticks.Value, TimeSpan.Zero));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(project =>
        {
            project.HasKey(p => p.Id);

            // Identity is created by the DOMAIN (Entity sets Guid.NewGuid()),
            // not by the database. EF must be told, or it assumes a non-default
            // key means the row already exists and issues an UPDATE that
            // matches nothing -- a DbUpdateConcurrencyException on first insert.
            project.Property(p => p.Id).ValueGeneratedNever();
            project.Property(p => p.Name).IsRequired().HasMaxLength(200);

            // The entity exposes IReadOnlyList<TaskItem>, so EF is pointed at
            // the backing field instead. Mapping to the property would require
            // a public setter and undo the encapsulation.
            project.HasMany(p => p.Tasks)
                .WithOne()
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            project.Navigation(p => p.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);

            project.Property(p => p.CreatedAt).HasConversion(DateTimeOffsetToTicks);

            // DomainEvents is behaviour, not state. It must never be persisted.
            project.Ignore(p => p.DomainEvents);
        });

        modelBuilder.Entity<TaskItem>(task =>
        {
            task.HasKey(t => t.Id);
            task.Property(t => t.Id).ValueGeneratedNever();
            task.Property(t => t.Title).IsRequired().HasMaxLength(200);
            task.Property(t => t.Description).HasMaxLength(2000);
            task.Property(t => t.Assignee).HasMaxLength(200);

            // State is stored as TEXT: it is only ever compared for equality,
            // and readable rows are worth having.
            task.Property(t => t.State).HasConversion<string>().HasMaxLength(20);

            // Priority is stored as its ORDINAL, deliberately. It is ORDERED --
            // both "order by priority descending" and the MinimumPriority
            // filter use >= on it. Stored as text, SQL compares lexicographic-
            // ally, so "High" lands below "Low" and the sort is silently wrong.
            //
            // The cost is that reordering the Priority enum would reinterpret
            // every existing row, so the member order is now part of the schema.
            // That is the trade: readable rows, or correct ordering.
            task.Property(t => t.Priority).HasConversion<int>();

            task.Property(t => t.CreatedAt).HasConversion(DateTimeOffsetToTicks);
            task.Property(t => t.CompletedAt).HasConversion(NullableDateTimeOffsetToTicks);

            task.Ignore(t => t.DomainEvents);

            task.HasIndex(t => t.ProjectId);
            task.HasIndex(t => t.State);
        });
    }
}
