using EfCoreLoggingAndHealthChecks.Data;
using EfCoreLoggingAndHealthChecks.Interceptors;
using EfCoreLoggingAndHealthChecks.Models;
using EfCoreLoggingAndHealthChecks.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLoggingAndHealthChecks.Tests;

/// <summary>
/// Exercises <see cref="AuditingSaveChangesInterceptor"/> directly against a real
/// Postgres, so these prove the actual round trip through SaveChangesAsync rather
/// than just in-memory change-tracker state.
/// </summary>
public class AuditingSaveChangesInterceptorTests : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public AuditingSaveChangesInterceptorTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(new AuditingSaveChangesInterceptor())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreatedAt_is_stamped_on_insert_without_the_caller_setting_it()
    {
        await using var db = CreateContext();

        var product = new Product { Name = $"Widget-{Guid.NewGuid():N}", Price = 9.99m };
        db.Products.Add(product);
        var before = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var after = DateTime.UtcNow;

        product.CreatedAt.Should().BeOnOrAfter(before.AddSeconds(-1)).And.BeOnOrBefore(after.AddSeconds(1));
        product.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdatedAt_is_stamped_on_modify_and_CreatedAt_is_left_untouched()
    {
        await using var db = CreateContext();
        var product = new Product { Name = $"Gadget-{Guid.NewGuid():N}", Price = 5m };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var originalCreatedAt = product.CreatedAt;

        product.Price = 6m;
        await db.SaveChangesAsync();

        product.CreatedAt.Should().Be(originalCreatedAt);
        product.UpdatedAt.Should().NotBeNull();
        product.UpdatedAt!.Value.Should().BeOnOrAfter(originalCreatedAt);
    }

    [Fact]
    public async Task A_row_that_is_never_modified_after_insert_keeps_a_null_UpdatedAt()
    {
        await using var db = CreateContext();
        var product = new Product { Name = $"Untouched-{Guid.NewGuid():N}", Price = 1m };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        await using var readBack = CreateContext();
        var stored = await readBack.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);

        stored.UpdatedAt.Should().BeNull();
    }
}
