using EfCoreLoggingAndHealthChecks.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EfCoreLoggingAndHealthChecks.HealthChecks;

/// <summary>
/// Reports Unhealthy when the database's schema doesn't match what the current
/// code expects -- i.e. there are migrations in the assembly that have not been
/// applied to this database yet.
///
/// This is a genuinely useful production pattern: a deploy that ships new code
/// without first running `dotnet ef database update` (or an equivalent migration
/// step) will fail confusingly at the first query that touches the missing
/// column/table, deep inside some unrelated request. Catching schema drift here
/// turns that into a clear, immediate readiness-probe failure instead.
/// </summary>
public class PendingMigrationsHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public PendingMigrationsHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = (await _dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken))
                .ToList();

            return pending.Count == 0
                ? HealthCheckResult.Healthy("no pending migrations")
                : HealthCheckResult.Unhealthy(
                    $"{pending.Count} pending migration(s): {string.Join(", ", pending)}");
        }
        catch (Exception ex)
        {
            // Most commonly: the database itself is unreachable. AddDbContextCheck
            // already reports that separately, but this check must not throw --
            // an unhandled exception here would take down the whole health report.
            return HealthCheckResult.Unhealthy("could not determine pending migrations", ex);
        }
    }
}
