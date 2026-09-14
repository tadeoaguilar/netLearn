namespace AdvancedDI.Challenge;

/// <summary>
/// The entry point for the Part 7 challenge: resolves the tenant from the
/// current scope, picks a channel, and sends. Every cross-cutting concern
/// (logging, retry, rate limiting) lives in decorators applied at registration
/// time, so this class stays a four-line method.
/// </summary>
public class NotificationService
{
    private readonly ITenantContext _tenantContext;
    private readonly INotificationFactory _factory;

    public NotificationService(ITenantContext tenantContext, INotificationFactory factory)
    {
        _tenantContext = tenantContext;
        _factory = factory;
    }

    public void Notify(NotificationChannel channel, string message)
    {
        var target = _factory.GetChannel(channel);
        target.Send(_tenantContext.TenantId, message);
    }

    /// <summary>
    /// Sends but reports rate-limit rejection as a result rather than an
    /// exception -- useful when a dropped notification is expected, not a bug.
    /// </summary>
    public bool TryNotify(NotificationChannel channel, string message)
    {
        try
        {
            Notify(channel, message);
            return true;
        }
        catch (RateLimitExceededException)
        {
            return false;
        }
    }
}
