namespace AdvancedDI.Challenge;

/// <summary>
/// Scoped: one tenant per scope, the same way one tenant belongs to one HTTP
/// request. Injecting this into a Singleton would be a captive dependency and
/// would leak one tenant's identity into another's notifications.
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
}

public class TenantContext : ITenantContext
{
    public TenantContext(string tenantId)
    {
        TenantId = tenantId;
    }

    public string TenantId { get; }
}
