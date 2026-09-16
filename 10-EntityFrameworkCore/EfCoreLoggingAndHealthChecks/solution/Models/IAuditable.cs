namespace EfCoreLoggingAndHealthChecks.Models;

/// <summary>
/// Marks an entity as carrying audit timestamps that
/// <see cref="Interceptors.AuditingSaveChangesInterceptor"/> stamps automatically on
/// every <c>SaveChanges</c>/<c>SaveChangesAsync</c> call. Neither <see cref="Product"/>
/// nor <see cref="Order"/> has to remember to set these -- the interceptor does it for
/// every tracked entity that implements this interface, the same way the snake_case
/// naming convention applies itself to every entity without anyone remembering to call
/// it per-type.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
