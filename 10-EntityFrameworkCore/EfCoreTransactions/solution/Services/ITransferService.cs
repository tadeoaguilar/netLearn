namespace EfCoreTransactions.Services;

/// <summary>
/// The public surface callers use to move money between accounts. Everything
/// this interface hides -- beginning a transaction, issuing the debit and
/// credit as separate <c>SaveChanges()</c> calls, committing or rolling back
/// -- is exactly the boilerplate Part 6 of EXERCISE.md asks you to factor out
/// once you notice every business operation from Parts 2-5 repeats it.
/// </summary>
public interface ITransferService
{
    /// <summary>
    /// Moves <paramref name="amount"/> from <paramref name="fromAccountId"/>
    /// to <paramref name="toAccountId"/> as a single atomic operation.
    /// Throws <see cref="InsufficientFundsException"/> if the source account
    /// can't cover it, in which case nothing is persisted -- not the debit,
    /// not the credit, not even the transfer record itself.
    /// </summary>
    Task<int> TransferAsync(
        int fromAccountId,
        int toAccountId,
        decimal amount,
        CancellationToken cancellationToken = default);
}
