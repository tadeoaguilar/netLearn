using Microsoft.Extensions.DependencyInjection;

namespace AdvancedDI.Challenge;

public interface INotificationFactory
{
    INotificationChannel GetChannel(NotificationChannel channel);
}

/// <summary>
/// Combines the two patterns from Parts 1 and 4: keyed registration does the
/// lookup, a factory wraps it so callers pass an enum instead of a magic string.
/// </summary>
public class NotificationFactory : INotificationFactory
{
    private readonly IServiceProvider _serviceProvider;

    public NotificationFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public INotificationChannel GetChannel(NotificationChannel channel)
        => _serviceProvider.GetRequiredKeyedService<INotificationChannel>(channel);
}
