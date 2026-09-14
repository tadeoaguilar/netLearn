using Microsoft.Extensions.DependencyInjection;

namespace AdvancedDI.Services;

/// <summary>
/// Part 4: three implementations of one interface, told apart by key.
/// </summary>
public interface INotificationService
{
    void Send(string message);
}

public class EmailNotificationService : INotificationService
{
    public void Send(string message) => Console.WriteLine($"[EMAIL] Sending: {message}");
}

public class SmsNotificationService : INotificationService
{
    public void Send(string message) => Console.WriteLine($"[SMS] Sending: {message}");
}

public class PushNotificationService : INotificationService
{
    public void Send(string message) => Console.WriteLine($"[PUSH] Sending: {message}");
}

/// <summary>
/// Part 4.3: the pre-.NET 8 alternative to keyed services. Prefer keyed
/// registration when you can -- this exists for older codebases and for cases
/// where the selection logic is more than a lookup.
/// </summary>
public class NotificationServiceResolver
{
    private readonly IServiceProvider _serviceProvider;

    public NotificationServiceResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public INotificationService GetService(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "email" => _serviceProvider.GetRequiredService<EmailNotificationService>(),
            "sms" => _serviceProvider.GetRequiredService<SmsNotificationService>(),
            "push" => _serviceProvider.GetRequiredService<PushNotificationService>(),
            _ => throw new ArgumentException($"Unknown notification type: {type}")
        };
    }
}
