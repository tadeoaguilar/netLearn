using Microsoft.EntityFrameworkCore;

namespace CatalogApi;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
