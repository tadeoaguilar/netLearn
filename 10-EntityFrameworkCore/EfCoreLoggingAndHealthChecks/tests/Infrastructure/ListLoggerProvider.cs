using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace EfCoreLoggingAndHealthChecks.Tests.Infrastructure;

/// <summary>
/// A minimal in-memory <see cref="ILoggerProvider"/> so tests can assert on what
/// got logged -- e.g. that <c>SlowQueryCommandInterceptor</c> actually wrote a
/// warning -- without parsing console output.
/// </summary>
public class ListLoggerProvider : ILoggerProvider
{
    public ConcurrentBag<(LogLevel Level, string Message)> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new ListLogger(this);

    public void Dispose()
    {
    }

    private class ListLogger(ListLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            owner.Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
