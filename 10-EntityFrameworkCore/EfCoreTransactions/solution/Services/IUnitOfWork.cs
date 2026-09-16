using System.Data;

namespace EfCoreTransactions.Services;

/// <summary>
/// Encapsulates "begin a transaction, run some work, commit on success,
/// roll back on failure" so callers stop repeating that boilerplate around
/// every multi-<c>SaveChanges()</c> operation. See Part 6 of EXERCISE.md,
/// which builds this after Parts 2-5 have shown why each piece exists.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside a database transaction at the
    /// given <paramref name="isolationLevel"/>. Commits if it completes
    /// normally; rolls back and rethrows if it throws anything, including a
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>.
    /// </summary>
    Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ExecuteAsync{T}(Func{Task{T}}, IsolationLevel, CancellationToken)"/>
    Task ExecuteAsync(
        Func<Task> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}
