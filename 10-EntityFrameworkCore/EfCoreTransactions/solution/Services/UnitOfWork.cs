using System.Data;
using EfCoreTransactions.Data;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Services;

public class UnitOfWork : IUnitOfWork
{
    private readonly BankDbContext _context;

    public UnitOfWork(BankDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(isolationLevel, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var result = await operation().ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            // Best-effort: if the connection is already broken (e.g. the
            // server killed it after a serialization failure) rolling back
            // explicitly can itself throw. Disposing the transaction still
            // guarantees it never commits.
            try
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Swallowed deliberately -- the original exception is what
                // the caller needs to see.
            }

            throw;
        }
    }

    public Task ExecuteAsync(
        Func<Task> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            },
            isolationLevel,
            cancellationToken);
    }
}
