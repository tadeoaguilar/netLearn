namespace EfCoreTransactions.Services;

/// <summary>
/// Thrown when a transfer would leave the source account's balance negative.
/// Deliberately a plain exception (not a result type) so Part 2's "simulate a
/// failure between the two SaveChanges calls" demo has something realistic to
/// throw, and Part 6's tests have something to assert rolls everything back.
/// </summary>
public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(int accountId, decimal requestedAmount, decimal availableBalance)
        : base($"Account {accountId} has insufficient funds: requested {requestedAmount:C}, available {availableBalance:C}.")
    {
        AccountId = accountId;
        RequestedAmount = requestedAmount;
        AvailableBalance = availableBalance;
    }

    public int AccountId { get; }

    public decimal RequestedAmount { get; }

    public decimal AvailableBalance { get; }
}
