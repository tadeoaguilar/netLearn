using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCoreModeling.Persistence;

/// <summary>
/// Part 1: the DbContext itself.
///
/// Why Fluent API (IEntityTypeConfiguration) instead of data annotations
/// like [Required]/[MaxLength] on the entities? Attributes tie the entity
/// class to EF Core: the moment Author references
/// System.ComponentModel.DataAnnotations, it is no longer a plain object
/// that happens to get persisted -- it is a class that KNOWS it is
/// persisted. Fluent API keeps that knowledge in this Persistence layer
/// instead, so every type in Entities/ stays a plain, framework-free POCO:
/// easy to unit test, easy to serialize, easy to reuse if the storage
/// technology ever changes.
/// </summary>
public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<DigitalBook> DigitalBooks => Set<DigitalBook>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<BookGenre> BookGenres => Set<BookGenre>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraryDbContext).Assembly);

        // Part 8: applied LAST, so it renames whatever the configurations
        // above produced -- columns, keys, foreign keys, indexes.
        modelBuilder.UseSnakeCaseNames();
    }
}
