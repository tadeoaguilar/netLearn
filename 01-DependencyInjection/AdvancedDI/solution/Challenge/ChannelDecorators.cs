using AdvancedDI.Configuration;
using Microsoft.Extensions.Options;

namespace AdvancedDI.Challenge;

/// <summary>
/// Records every delivery attempt. Registered as a Singleton so the history
/// survives across scopes and tenants.
/// </summary>
public interface INotificationLogger
{
    void Record(string tenantId, string channel, string message, bool succeeded);
    IReadOnlyList<string> History { get; }
}

public class InMemoryNotificationLogger : INotificationLogger
{
    private readonly List<string> _history = new();
    private readonly Lock _gate = new();

    public IReadOnlyList<string> History
    {
        get { lock (_gate) return _history.ToArray(); }
    }

    public void Record(string tenantId, string channel, string message, bool succeeded)
    {
        var outcome = succeeded ? "OK" : "FAILED";
        lock (_gate) _history.Add($"{tenantId}|{channel}|{message}|{outcome}");
    }
}

/// <summary>Logs each attempt without changing delivery behaviour.</summary>
public class LoggingChannelDecorator : INotificationChannel
{
    private readonly INotificationChannel _inner;
    private readonly INotificationLogger _logger;

    public LoggingChannelDecorator(INotificationChannel inner, INotificationLogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public string Name => _inner.Name;

    public void Send(string tenantId, string message)
    {
        try
        {
            _inner.Send(tenantId, message);
            _logger.Record(tenantId, Name, message, succeeded: true);
        }
        catch
        {
            _logger.Record(tenantId, Name, message, succeeded: false);
            throw; // Logging must not swallow the failure -- retry sits outside.
        }
    }
}

/// <summary>
/// Retries a failed send. Place this OUTSIDE the logging decorator so that each
/// individual attempt is logged; place it inside and you only ever see one.
/// </summary>
public class RetryChannelDecorator : INotificationChannel
{
    private readonly INotificationChannel _inner;
    private readonly int _maxRetries;

    public RetryChannelDecorator(INotificationChannel inner, IOptions<NotificationSettings> settings)
        : this(inner, settings.Value.MaxRetries)
    {
    }

    public RetryChannelDecorator(INotificationChannel inner, int maxRetries)
    {
        _inner = inner;
        _maxRetries = maxRetries;
    }

    public string Name => _inner.Name;

    public void Send(string tenantId, string message)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _inner.Send(tenantId, message);
                return;
            }
            catch (Exception ex) when (attempt <= _maxRetries)
            {
                Console.WriteLine($"  [RETRY] attempt {attempt} failed: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// Sliding-window rate limit. Time comes from an injected
/// <see cref="TimeProvider"/>, so a test can advance the clock instead of
/// sleeping for a minute.
/// </summary>
public class RateLimitingChannelDecorator : INotificationChannel
{
    private readonly INotificationChannel _inner;
    private readonly int _maxPerMinute;
    private readonly TimeProvider _timeProvider;
    private readonly Queue<DateTimeOffset> _sent = new();

    public RateLimitingChannelDecorator(
        INotificationChannel inner,
        IOptions<NotificationSettings> settings,
        TimeProvider timeProvider)
        : this(inner, settings.Value.MaxPerMinute, timeProvider)
    {
    }

    public RateLimitingChannelDecorator(
        INotificationChannel inner,
        int maxPerMinute,
        TimeProvider timeProvider)
    {
        _inner = inner;
        _maxPerMinute = maxPerMinute;
        _timeProvider = timeProvider;
    }

    public string Name => _inner.Name;

    public void Send(string tenantId, string message)
    {
        var now = _timeProvider.GetUtcNow();

        while (_sent.Count > 0 && now - _sent.Peek() >= TimeSpan.FromMinutes(1))
        {
            _sent.Dequeue();
        }

        if (_sent.Count >= _maxPerMinute)
        {
            Console.WriteLine($"  [RATE LIMIT] dropped for {tenantId}: {message}");
            throw new RateLimitExceededException(
                $"Rate limit of {_maxPerMinute}/minute exceeded for {Name}");
        }

        _sent.Enqueue(now);
        _inner.Send(tenantId, message);
    }
}

public class RateLimitExceededException : Exception
{
    public RateLimitExceededException(string message) : base(message) { }
}
