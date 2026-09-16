using EfCoreTransactions.Data;
using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Services;

public class TransferService : ITransferService
{
    private readonly BankDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public TransferService(BankDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public Task<int> TransferAsync(
        int fromAccountId,
        int toAccountId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Transfer amount must be positive.");
        }

        if (fromAccountId == toAccountId)
        {
            throw new ArgumentException("An account cannot transfer to itself.", nameof(toAccountId));
        }

        // Everything below runs inside ONE database transaction, courtesy of
        // UnitOfWork.ExecuteAsync. If InsufficientFundsException (or
        // anything else) is thrown at any point, the transaction rolls back
        // and none of it persists -- not the debit, not the credit, not even
        // the Transfer row's initial Pending insert. That is the trade-off
        // of folding the whole operation into one atomic unit: there is no
        // audit trail of a failed attempt inside the database. Part 2's demo
        // shows the alternative -- logging a Failed transfer in a separate
        // transaction, after the real one has already rolled back.
        return _unitOfWork.ExecuteAsync(async () =>
        {
            var transfer = new Transfer
            {
                FromAccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = amount,
                Status = TransferStatus.Pending
            };
            _context.Transfers.Add(transfer);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var from = await _context.Accounts
                .SingleAsync(a => a.Id == fromAccountId, cancellationToken)
                .ConfigureAwait(false);

            if (from.Balance < amount)
            {
                throw new InsufficientFundsException(fromAccountId, amount, from.Balance);
            }

            from.Balance -= amount;
            // First SaveChanges(): the debit leg, on its own -- exactly the
            // shape Part 2 warns about, except now it's safe because it's
            // wrapped in a transaction.
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var to = await _context.Accounts
                .SingleAsync(a => a.Id == toAccountId, cancellationToken)
                .ConfigureAwait(false);
            to.Balance += amount;
            // Second SaveChanges(): the credit leg.
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            transfer.Status = TransferStatus.Completed;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return transfer.Id;
        }, cancellationToken: cancellationToken);
    }
}
