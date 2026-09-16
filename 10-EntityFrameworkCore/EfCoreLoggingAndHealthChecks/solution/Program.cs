using System.Text.Json;
using EfCoreLoggingAndHealthChecks.Data;
using EfCoreLoggingAndHealthChecks.HealthChecks;
using EfCoreLoggingAndHealthChecks.Interceptors;
using EfCoreLoggingAndHealthChecks.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// The interceptors are registered as services so they can take dependencies
// (SlowQueryCommandInterceptor needs an ILogger) instead of being constructed
// by hand with `new`. Singleton: interceptors are stateless here and EF Core
// reuses the same instance across every DbContext instance created from these
// options, which is the documented, recommended lifetime for them.
builder.Services.AddSingleton<AuditingSaveChangesInterceptor>();
builder.Services.AddSingleton<SlowQueryCommandInterceptor>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException(
            "Missing 'ConnectionStrings:Default' configuration value.");

    options.UseNpgsql(connectionString);

    // Part 5: EF Core's own internal logs (SQL, connection open/close, change
    // detection, ...) flow through the SAME Microsoft.Extensions.Logging
    // pipeline as the rest of the app -- same categories, same levels, same
    // structured output -- instead of a separate Console.WriteLine sink like
    // Part 1's LogTo(). This is what Part 1 grows into in a real app.
    options.UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>());

    // Part 3 + Part 4: audit stamping and slow-query detection, wired in once
    // here rather than called manually from every endpoint.
    options.AddInterceptors(
        serviceProvider.GetRequiredService<AuditingSaveChangesInterceptor>(),
        serviceProvider.GetRequiredService<SlowQueryCommandInterceptor>());

    if (builder.Environment.IsDevelopment())
    {
        // Part 2: reveals actual bound parameter VALUES (not just placeholders)
        // in logs and exception messages, plus full detail on exceptions that
        // would otherwise be generic. Genuinely useful while developing, and a
        // real compliance problem if it ever reaches a production log
        // aggregator -- PII and secrets end up in plain text, searchable by
        // anyone with log access, and retained far longer than the request
        // that logged them. Gated behind IsDevelopment() for exactly that
        // reason; never enable this unconditionally.
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Part 6 + Part 7: two probes, three checks between them, told apart by tag.
//
//   "ready" -- AddDbContextCheck<AppDbContext> (can the database actually be
//   reached?) and PendingMigrationsHealthCheck (does its schema match what
//   this build of the code expects?). Either failing means traffic should
//   stop being routed here.
//
//   "live" -- a no-dependency check. It only proves the process itself is up
//   and able to answer HTTP requests; it deliberately never touches the
//   database, so a database outage does not make an orchestrator kill and
//   restart every instance (which would fix nothing and drop in-flight work).
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
    .AddCheck<PendingMigrationsHealthCheck>("pending-migrations", tags: ["ready"])
    .AddCheck("self", () => HealthCheckResult.Healthy("process is up"), tags: ["live"]);

var app = builder.Build();

// Deliberately NOT calling Database.Migrate() here. A production app that
// migrates on every boot dies at startup the moment the database is briefly
// unreachable -- before health checks even get a chance to report *why*, and
// before the orchestrator can tell "starting up" apart from "broken". Apply
// migrations as an explicit deploy step instead:
//     dotnet ef database update --project solution
// (the tests project does the equivalent against its Testcontainers Postgres).

static Task WriteHealthReport(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description
        })
    }));
}

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthReport
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthReport
});

app.MapGet("/", () => Results.Ok(new
{
    service = "EfCoreLoggingAndHealthChecks",
    probes = new[] { "/health/ready", "/health/live" },
    endpoints = new[] { "GET /products", "POST /products", "GET /orders", "POST /orders" }
}));

// Trivial endpoints whose only job is to generate real EF Core activity --
// inserts and reads to watch the logging/interceptor parts observe.
app.MapGet("/products", async (AppDbContext db) =>
    Results.Ok(await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync()));

app.MapPost("/products", async (Product product, AppDbContext db) =>
{
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{product.Id}", product);
});

app.MapGet("/orders", async (AppDbContext db) =>
    Results.Ok(await db.Orders.AsNoTracking().OrderBy(o => o.Id).ToListAsync()));

app.MapPost("/orders", async (CreateOrderRequest request, AppDbContext db) =>
{
    if (request.Quantity <= 0)
    {
        return Results.BadRequest(new { error = "Quantity must be positive." });
    }

    var productExists = await db.Products.AnyAsync(p => p.Id == request.ProductId);
    if (!productExists)
    {
        return Results.NotFound(new { error = $"Product {request.ProductId} does not exist." });
    }

    var order = new Order { ProductId = request.ProductId, Quantity = request.Quantity };
    db.Orders.Add(order);
    await db.SaveChangesAsync();

    return Results.Created($"/orders/{order.Id}", order);
});

app.Run();

public partial class Program;
