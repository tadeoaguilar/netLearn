using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser currentUser,
        IDateTimeProvider clock) : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public DbSet<Project> ProjectSet => Set<Project>();
    public DbSet<TaskItem> TaskSet => Set<TaskItem>();

    // The Application layer sees IQueryable, not DbSet, so it never gains
    // access to Add/Remove/Attach or anything else EF-specific.
    IQueryable<Project> IApplicationDbContext.Projects => ProjectSet.Include(p => p.Tasks).AsNoTracking();
    IQueryable<TaskItem> IApplicationDbContext.Tasks => TaskSet.AsNoTracking();

    public async Task<Project?> FindProjectAsync(Guid id, CancellationToken cancellationToken = default)
        => await ProjectSet.Include(p => p.Tasks).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<TaskItem?> FindTaskAsync(Guid id, CancellationToken cancellationToken = default)
        => await TaskSet.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void AddProject(Project project) => ProjectSet.Add(project);
    public void RemoveProject(Project project) => ProjectSet.Remove(project);

    /// <summary>
    /// Stamps audit fields and clears domain events on every save.
    ///
    /// Doing it here rather than in each handler is the difference between an
    /// audit trail you can trust and one where half the rows have no CreatedBy
    /// because somebody forgot.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var user = _currentUser.UserId ?? "system";

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = user;
                    break;
                case EntityState.Modified:
                    entry.Entity.LastModifiedAt = now;
                    entry.Entity.LastModifiedBy = user;
                    break;
            }
        }

        var written = await base.SaveChangesAsync(cancellationToken);

        foreach (var entity in ChangeTracker.Entries<BaseEntity>().Select(e => e.Entity))
        {
            entity.ClearDomainEvents();
        }

        return written;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Applied LAST, so it renames whatever the configurations produced.
        // Tables are already named explicitly; this covers columns, keys,
        // foreign keys and indexes.
        modelBuilder.UseSnakeCaseNames();
    }
}
