using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CloudNative.HealthChecks;

/// <summary>
/// Simulated dependencies whose health can be toggled at runtime, so the three
/// probe types can be seen doing different things.
/// </summary>
public class DependencyState
{
    public bool DatabaseUp { get; set; } = true;
    public bool CacheUp { get; set; } = true;
    public bool WarmedUp { get; set; }
}

/// <summary>
/// A hard dependency. If the database is down the instance cannot serve
/// traffic, so this belongs in the READINESS probe.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly DependencyState _state;

    public DatabaseHealthCheck(DependencyState state) => _state = state;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(_state.DatabaseUp
            ? HealthCheckResult.Healthy("database reachable")
            : HealthCheckResult.Unhealthy("database unreachable"));
}

/// <summary>
/// A soft dependency. Losing the cache makes the service slower, not broken, so
/// it reports DEGRADED rather than Unhealthy.
///
/// Getting this distinction wrong is how a cache outage turns into a full
/// outage: mark it Unhealthy and the orchestrator pulls every healthy instance
/// out of the load balancer.
/// </summary>
public class CacheHealthCheck : IHealthCheck
{
    private readonly DependencyState _state;

    public CacheHealthCheck(DependencyState state) => _state = state;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(_state.CacheUp
            ? HealthCheckResult.Healthy("cache reachable")
            : HealthCheckResult.Degraded("cache unreachable -- serving from source, slower"));
}

/// <summary>
/// Startup work: migrations, cache warming, JIT. Separating this from readiness
/// lets a slow-starting app take its time without the orchestrator killing it
/// for failing a liveness probe it was never going to pass yet.
/// </summary>
public class StartupHealthCheck : IHealthCheck
{
    private readonly DependencyState _state;

    public StartupHealthCheck(DependencyState state) => _state = state;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(_state.WarmedUp
            ? HealthCheckResult.Healthy("warm-up complete")
            : HealthCheckResult.Unhealthy("still warming up"));
}
