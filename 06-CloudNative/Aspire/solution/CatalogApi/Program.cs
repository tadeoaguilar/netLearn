using CatalogApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Wires up telemetry, health checks, service discovery and HTTP resilience
// -- see ServiceDefaults/Extensions.cs. Every project in this exercise
// calls this first.
builder.AddServiceDefaults();

// Aspire client integration: registers CatalogDbContext AND a health check
// AND OpenTelemetry instrumentation for Npgsql, all from one call. Compare
// this to 10-EntityFrameworkCore, where each of those was wired by hand.
builder.AddNpgsqlDbContext<CatalogDbContext>("catalogdb");

// Same story for Redis: one call registers the output cache, a health
// check, and telemetry.
builder.AddRedisOutputCache("cache");

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseOutputCache();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.EnsureCreatedAsync();

    if (!await db.Products.AnyAsync())
    {
        // AppHost passes this in via builder.AddParameter("seed-product-count"),
        // demonstrating Aspire's parameter resource -- see EXERCISE.md Part A.6.
        var seedCount = builder.Configuration.GetValue("SeedProductCount", 10);

        for (var i = 1; i <= seedCount; i++)
        {
            db.Products.Add(new Product { Name = $"Product {i}", Price = i * 4.5m });
        }

        await db.SaveChangesAsync();
    }
}

app.MapGet("/products", async (CatalogDbContext db) =>
    await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync())
    .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(30)).Tag("products"));

app.MapGet("/products/{id:int}", async (int id, CatalogDbContext db) =>
    await db.Products.FindAsync(id) is { } product
        ? Results.Ok(product)
        : Results.NotFound());

app.Run();

public partial class Program;
