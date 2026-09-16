namespace EfCoreTransactions.Domain;

/// <summary>
/// A bank account. <see cref="Version"/> is not a business field -- it is
/// mapped (see <c>AccountConfiguration</c>) onto PostgreSQL's <c>xmin</c>
/// system column, which the server increments on every UPDATE of the row.
/// EF Core uses it as an optimistic concurrency token automatically: it gets
/// included in the WHERE clause of every UPDATE, so a stale write affects
/// zero rows and EF Core raises <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
/// instead of silently clobbering someone else's change. See Part 4.
/// </summary>
public class Account
{
    public int Id { get; set; }

    public string Owner { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    /// <summary>
    /// Postgres's <c>xmin</c> system column, surfaced as the concurrency
    /// token. Never set this yourself -- the database and EF Core manage it.
    /// </summary>
    public uint Version { get; set; }
}
