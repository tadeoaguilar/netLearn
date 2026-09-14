using CloudNative.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CloudNative.Tests;

/// <summary>
/// The distinction these tests protect: liveness, readiness and startup answer
/// three different questions, and conflating them causes outages.
/// </summary>
public class HealthCheckTests
{
    private static HealthCheckContext Context() => new();

    [Fact]
    public async Task A_hard_dependency_going_down_reports_Unhealthy()
    {
        var state = new DependencyState { DatabaseUp = false };

        var result = await new DatabaseHealthCheck(state).CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task A_soft_dependency_going_down_reports_Degraded_not_Unhealthy()
    {
        // The important one. Report a cache outage as Unhealthy and the
        // orchestrator pulls every instance out of the load balancer, turning
        // a slowdown into a full outage.
        var state = new DependencyState { CacheUp = false };

        var result = await new CacheHealthCheck(state).CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("slower");
    }

    [Fact]
    public async Task The_startup_check_fails_until_warm_up_completes()
    {
        var state = new DependencyState { WarmedUp = false };
        var check = new StartupHealthCheck(state);

        (await check.CheckHealthAsync(Context())).Status.Should().Be(HealthStatus.Unhealthy);

        state.WarmedUp = true;
        (await check.CheckHealthAsync(Context())).Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Health_checks_report_independently_of_each_other()
    {
        var state = new DependencyState { DatabaseUp = true, CacheUp = false };

        var database = await new DatabaseHealthCheck(state).CheckHealthAsync(Context());
        var cache = await new CacheHealthCheck(state).CheckHealthAsync(Context());

        database.Status.Should().Be(HealthStatus.Healthy);
        cache.Status.Should().Be(HealthStatus.Degraded);
    }
}
