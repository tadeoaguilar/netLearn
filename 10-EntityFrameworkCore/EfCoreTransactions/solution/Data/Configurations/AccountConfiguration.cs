using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreTransactions.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        // A real CHECK constraint, enforced by Postgres itself -- Part 3's
        // savepoint demo deliberately violates it to show how a single
        // failed statement can be undone without aborting the whole
        // transaction. The SQL text names the FINAL (post snake_case)
        // column, since SnakeCaseNaming only renames metadata, not raw SQL.
        builder.ToTable("accounts", t => t.HasCheckConstraint("ck_accounts_balance_non_negative", "balance >= 0"));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Owner)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Balance)
            .HasColumnType("decimal(18,2)");

        // Map the CLR property onto Postgres's xmin system column and tell EF
        // Core to treat it as a concurrency token. xmin is a 32-bit unsigned
        // transaction id, hence uint + the "xid" column type -- see Npgsql's
        // "Concurrency Tokens" documentation. The column is server-managed:
        // ValueGeneratedOnAddOrUpdate means EF Core never writes to it, only
        // reads it back and compares it on the next UPDATE/DELETE.
        builder.Property(a => a.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
