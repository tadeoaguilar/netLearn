using Npgsql;

namespace EfCoreTransactions.Services;

/// <summary>
/// Detects and retries Postgres serialization failures (SQLSTATE 40001),
/// which a SERIALIZABLE transaction can throw at COMMIT time -- or at any
/// statement -- when the server cannot guarantee the transaction ran as if it
/// were alone. See Part 5 of EXERCISE.md.
///
/// The standard advice for SERIALIZABLE in Postgres is: catch 40001, throw
/// the whole transaction away (nothing it did is trustworthy), and retry from
/// the top. This class is that loop, factored out so Part 5's demo and
/// <see cref="TransferService"/> don't each reimplement it.
/// </summary>
public static class SerializationRetryPolicy
{
    /// <summary>
    /// The SQLSTATE Postgres uses for "could not serialize access due to
    /// concurrent update" and similar serialization failures.
    /// </summary>
    public const string SerializationFailureSqlState = "40001";

    public static bool IsSerializationFailure(Exception exception) =>
        FindPostgresException(exception) is { SqlState: SerializationFailureSqlState };

    private static PostgresException? FindPostgresException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is PostgresException postgresException)
            {
                return postgresException;
            }

            exception = exception.InnerException;
        }

        return null;
    }

    /// <summary>
    /// Runs <paramref name="operation"/>, retrying it (from scratch) whenever
    /// it fails with a 40001 serialization failure, up to
    /// <paramref name="maxAttempts"/> times total.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Must retry at least once.");
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsSerializationFailure(ex) && attempt < maxAttempts)
            {
                // A short, jittered backoff gives the transaction that "won"
                // a chance to commit before we compete with it again.
                var delay = TimeSpan.FromMilliseconds(25 * attempt);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc cref="ExecuteAsync{T}(Func{Task{T}}, int, CancellationToken)"/>
    public static async Task ExecuteAsync(
        Func<Task> operation,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            },
            maxAttempts,
            cancellationToken).ConfigureAwait(false);
    }
}
