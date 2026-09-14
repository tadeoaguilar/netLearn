namespace AdvancedDI.Challenge;

public enum NotificationChannel
{
    Email,
    Sms,
    Push,
    Slack
}

/// <summary>
/// A single delivery channel. Implementations throw on failure; the retry
/// decorator is what turns a transient failure into a successful send.
/// </summary>
public interface INotificationChannel
{
    string Name { get; }
    void Send(string tenantId, string message);
}

public class EmailChannel : INotificationChannel
{
    public string Name => "Email";

    public void Send(string tenantId, string message)
        => Console.WriteLine($"  [Email] ({tenantId}) {message}");
}

public class SmsChannel : INotificationChannel
{
    public string Name => "SMS";

    public void Send(string tenantId, string message)
        => Console.WriteLine($"  [SMS] ({tenantId}) {message}");
}

public class PushChannel : INotificationChannel
{
    public string Name => "Push";

    public void Send(string tenantId, string message)
        => Console.WriteLine($"  [Push] ({tenantId}) {message}");
}

/// <summary>
/// Deliberately unreliable, so the retry decorator has something to do.
/// The failure count is a constructor parameter rather than a hard-coded
/// random, which is what lets a test pin the behaviour down exactly.
/// </summary>
public class SlackChannel : INotificationChannel
{
    private readonly int _failuresBeforeSuccess;
    private int _attempts;

    public SlackChannel(int failuresBeforeSuccess = 2)
    {
        _failuresBeforeSuccess = failuresBeforeSuccess;
    }

    public string Name => "Slack";

    public int Attempts => _attempts;

    public void Send(string tenantId, string message)
    {
        _attempts++;

        if (_attempts <= _failuresBeforeSuccess)
        {
            throw new InvalidOperationException(
                $"Slack API unavailable (attempt {_attempts})");
        }

        Console.WriteLine($"  [Slack] ({tenantId}) {message}");
    }
}
