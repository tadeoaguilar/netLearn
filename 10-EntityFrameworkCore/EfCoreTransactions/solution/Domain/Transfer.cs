namespace EfCoreTransactions.Domain;

/// <summary>
/// A record of money moved from one <see cref="Account"/> to another. Kept as
/// its own row (rather than just mutating two balances) so the exercises have
/// something to inspect after a rollback: a <see cref="TransferStatus.Failed"/>
/// row with untouched balances tells the story of what almost happened.
/// </summary>
public class Transfer
{
    public int Id { get; set; }

    public int FromAccountId { get; set; }

    public int ToAccountId { get; set; }

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TransferStatus Status { get; set; } = TransferStatus.Pending;
}
