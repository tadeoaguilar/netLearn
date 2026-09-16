using EfCoreLoggingAndHealthChecks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLoggingAndHealthChecks.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditable.CreatedAt"/>/<see cref="IAuditable.UpdatedAt"/> on
/// every save, for every tracked entity that implements <see cref="IAuditable"/>.
///
/// Doing this in an interceptor -- rather than in each endpoint handler -- means
/// nobody can forget to set the timestamp. It runs for every SaveChanges call from
/// anywhere in the app, including code written after today.
/// </summary>
public class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        StampAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StampAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void StampAuditFields(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    // CreatedAt should never change after the row was inserted,
                    // no matter what the caller assigned to the tracked entity.
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    break;
            }
        }
    }
}
