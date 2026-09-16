using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EfCoreLoggingAndHealthChecks.Interceptors;

/// <summary>
/// A simple, real slow-query detector: EF Core hands every executed command's
/// elapsed time to <see cref="CommandExecutedEventData.Duration"/>, so no manual
/// <see cref="System.Diagnostics.Stopwatch"/> bookkeeping is needed. Anything at or
/// above <see cref="SlowQueryThreshold"/> gets logged as a warning, with the SQL
/// text, so a slow endpoint shows up in ordinary application logs instead of
/// requiring a profiler session to notice.
/// </summary>
public class SlowQueryCommandInterceptor : DbCommandInterceptor
{
    public static readonly TimeSpan SlowQueryThreshold = TimeSpan.FromMilliseconds(500);

    private readonly ILogger<SlowQueryCommandInterceptor> _logger;

    public SlowQueryCommandInterceptor(ILogger<SlowQueryCommandInterceptor> logger)
    {
        _logger = logger;
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        LogIfSlow(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        LogIfSlow(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void LogIfSlow(DbCommand command, CommandExecutedEventData eventData)
    {
        if (eventData.Duration < SlowQueryThreshold) return;

        _logger.LogWarning(
            "Slow query detected ({DurationMs}ms, threshold {ThresholdMs}ms): {CommandText}",
            eventData.Duration.TotalMilliseconds,
            SlowQueryThreshold.TotalMilliseconds,
            command.CommandText);
    }
}
