using EfCoreQuerying.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Persistence;

public class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(entity =>
        {
            entity.ToTable("authors");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Publisher>(entity =>
        {
            entity.ToTable("publishers");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("genres");
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(g => g.Name).IsUnique();
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.ToTable("books");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Title).IsRequired().HasMaxLength(300);
            entity.Property(b => b.Isbn).IsRequired().HasMaxLength(20);
            entity.HasIndex(b => b.Isbn).IsUnique();
            entity.Property(b => b.Price).HasColumnType("numeric(10,2)");

            // Native Postgres array column -- no value converter required,
            // the Npgsql provider maps string[] to text[] by convention.
            entity.Property(b => b.Tags).HasColumnType("text[]");

            // Native Postgres jsonb column, stored as text in the CLR model.
            entity.Property(b => b.Metadata).HasColumnType("jsonb");

            entity.HasOne(b => b.Author)
                .WithMany(a => a.Books)
                .HasForeignKey(b => b.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Publisher)
                .WithMany(p => p.Books)
                .HasForeignKey(b => b.PublisherId)
                .OnDelete(DeleteBehavior.Restrict);

            // Implicit many-to-many: EF Core creates the "book_genre" join
            // table for us because neither side carries extra columns.
            entity.HasMany(b => b.Genres)
                .WithMany(g => g.Books)
                .UsingEntity(j => j.ToTable("book_genre"));
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Comment).HasMaxLength(2000);

            entity.HasOne(r => r.Book)
                .WithMany(b => b.Reviews)
                .HasForeignKey(r => r.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(t => t.HasCheckConstraint("ck_reviews_rating_range", "rating between 1 and 5"));
        });

        // Applied last so it renames everything the configuration above set up,
        // including the auto-generated "book_genre" join table's own columns.
        modelBuilder.UseSnakeCaseNames();
    }
}
