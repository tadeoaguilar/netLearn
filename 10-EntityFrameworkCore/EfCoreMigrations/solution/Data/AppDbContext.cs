using EfCoreMigrations.Models;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMigrations.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Genre> Genres => Set<Genre>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Applied LAST, so it renames whatever the configurations produced.
        // Tables are already named explicitly; this covers columns, keys,
        // foreign keys and indexes.
        modelBuilder.UseSnakeCaseNames();
    }
}
