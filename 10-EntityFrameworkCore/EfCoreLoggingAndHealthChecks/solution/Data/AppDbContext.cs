using EfCoreLoggingAndHealthChecks.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLoggingAndHealthChecks.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.Property(o => o.Quantity).IsRequired();

            entity.HasOne(o => o.Product)
                .WithMany()
                .HasForeignKey(o => o.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Applied last so it renames whatever the configurations above produced,
        // the same ordering module 09's ApplicationDbContext uses.
        modelBuilder.UseSnakeCaseNames();

        base.OnModelCreating(modelBuilder);
    }
}
