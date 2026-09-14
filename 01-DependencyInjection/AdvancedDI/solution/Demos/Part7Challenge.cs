using AdvancedDI.Challenge;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdvancedDI.Demos;

/// <summary>
/// The Part 7 challenge solution: every pattern from Parts 1-6 combined into
/// one multi-tenant notification system.
/// </summary>
public static class Part7Challenge
{
    public static void Run()
    {
        Console.WriteLine("=== PART 7: MULTI-TENANT NOTIFICATION SYSTEM ===\n");

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile("appsettings.json", optional: false);
        builder.Services.AddNotifications(builder.Configuration);

        var host = builder.Build();

        foreach (var tenant in new[] { "acme-corp", "globex" })
        {
            Console.WriteLine($"\n--- Tenant: {tenant} ---");

            // One scope per tenant, exactly as a web host creates one per request.
            using var scope = host.Services.CreateScope();
            var notifications = BuildServiceForTenant(scope.ServiceProvider, tenant);

            notifications.Notify(NotificationChannel.Email, "Your invoice is ready");
            notifications.Notify(NotificationChannel.Sms, "Code: 4821");

            Console.WriteLine("\n  Slack is flaky -- watch the retry decorator:");
            notifications.Notify(NotificationChannel.Slack, "Deployment finished");

            // Worth noticing: the SECOND tenant's Slack send succeeds on the
            // first attempt. The channel is registered as a Singleton, so its
            // attempt counter is shared by every tenant -- state in a Singleton
            // is application-wide, never per-scope. Here that is harmless; if
            // the field held a tenant's API token instead, it would be a data
            // leak. This is the captive-dependency lesson from DILifetimes
            // showing up in a realistic object graph.
        }

        Console.WriteLine("\n--- Rate limiting (3/minute, from appsettings.json) ---");
        using (var scope = host.Services.CreateScope())
        {
            var notifications = BuildServiceForTenant(scope.ServiceProvider, "noisy-tenant");

            for (var i = 1; i <= 5; i++)
            {
                var sent = notifications.TryNotify(NotificationChannel.Push, $"Ping {i}");
                if (!sent)
                {
                    Console.WriteLine($"  Ping {i} was rate limited");
                }
            }
        }

        Console.WriteLine("\n--- Audit trail (Singleton, spans every tenant) ---");
        var logger = host.Services.GetRequiredService<INotificationLogger>();
        foreach (var entry in logger.History)
        {
            Console.WriteLine($"  {entry}");
        }
    }

    /// <summary>
    /// Swaps in the real tenant for this scope. In a web application an
    /// ITenantContext implementation would read the tenant from the request
    /// instead; the rest of the graph is identical either way.
    /// </summary>
    private static NotificationService BuildServiceForTenant(IServiceProvider provider, string tenantId)
        => new(new TenantContext(tenantId), provider.GetRequiredService<INotificationFactory>());
}
