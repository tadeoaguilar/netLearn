using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Data;

public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankDbContext).Assembly);

        // Applied LAST, so it renames whatever the configurations produced.
        // Tables are already named explicitly; this covers columns, keys,
        // foreign keys and indexes -- including turning "xmin" into "xmin"
        // (a no-op, since it's already snake_case and already lower-cased by
        // Postgres) and "FromAccountId" into "from_account_id".
        modelBuilder.UseSnakeCaseNames();
    }
}
