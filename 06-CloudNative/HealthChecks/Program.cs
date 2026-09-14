using System.Text.Json;
using CloudNative.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<DependencyState>();

// Tags are what let ONE set of checks serve THREE different probes.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<CacheHealthCheck>("cache", tags: ["ready"])
    .AddCheck<StartupHealthCheck>("startup", tags: ["startup"]);

var app = builder.Build();

// Simulated start-up work (migrations, cache warming). Configurable so the
// startup probe can actually be observed failing before it passes:
//     WarmupSeconds=10 dotnet run
var warmupSeconds = app.Configuration.GetValue("WarmupSeconds", 5);

_ = Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(warmupSeconds));
    app.Services.GetRequiredService<DependencyState>().WarmedUp = true;
    app.Logger.LogInformation("warm-up complete after {Seconds}s", warmupSeconds);
});

static Task WriteJson(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description
        })
    }));
}

// LIVENESS: "is this process wedged?" It must NOT check dependencies -- a
// database outage would otherwise make Kubernetes restart every healthy pod,
// which fixes nothing and loses all in-flight work.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteJson
});

// READINESS: "should traffic be routed here?" Checks hard dependencies. A
// failing instance is removed from the load balancer but left running, so it
// can recover and rejoin.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteJson,

    // Degraded still returns 200: the instance is slower, not broken, and
    // pulling it from rotation would make things worse.
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

// STARTUP: "has initialisation finished?" Once this passes, the orchestrator
// switches to the liveness probe.
app.MapHealthChecks("/health/startup", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("startup"),
    ResponseWriter = WriteJson
});

app.MapGet("/", () => Results.Ok(new
{
    service = "Health checks",
    probes = new[] { "/health/live", "/health/ready", "/health/startup" },
    controls = new[] { "POST /toggle/database", "POST /toggle/cache" }
}));

// Toggles so the probes can be watched changing.
app.MapPost("/toggle/database", (DependencyState state) =>
{
    state.DatabaseUp = !state.DatabaseUp;
    return Results.Ok(new { databaseUp = state.DatabaseUp });
});

app.MapPost("/toggle/cache", (DependencyState state) =>
{
    state.CacheUp = !state.CacheUp;
    return Results.Ok(new { cacheUp = state.CacheUp });
});

app.Run();

public partial class Program;
