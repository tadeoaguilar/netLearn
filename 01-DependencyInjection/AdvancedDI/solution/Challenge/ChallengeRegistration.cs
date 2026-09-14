using AdvancedDI.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdvancedDI.Challenge;

/// <summary>
/// All the wiring for the Part 7 challenge in one extension method.
///
/// Keeping registration in an extension method next to the feature -- rather
/// than in Program.cs -- is how real applications stop Program.cs from growing
/// to hundreds of lines, and it is what makes the same wiring reusable from a
/// test project.
/// </summary>
public static class ChallengeRegistration
{
    public static IServiceCollection AddNotifications(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<NotificationSettings>(
            configuration.GetSection(NotificationSettings.SectionName));

        services.TryAddTimeProvider();

        // Singleton: the audit trail must outlive any one tenant scope.
        services.AddSingleton<INotificationLogger, InMemoryNotificationLogger>();

        // Scoped: one tenant per scope.
        services.AddScoped<ITenantContext>(_ => new TenantContext("unknown-tenant"));

        services.AddSingleton<INotificationFactory, NotificationFactory>();
        services.AddScoped<NotificationService>();

        AddDecoratedChannel(services, NotificationChannel.Email, _ => new EmailChannel());
        AddDecoratedChannel(services, NotificationChannel.Sms, _ => new SmsChannel());
        AddDecoratedChannel(services, NotificationChannel.Push, _ => new PushChannel());
        AddDecoratedChannel(services, NotificationChannel.Slack, _ => new SlackChannel());

        return services;
    }

    /// <summary>
    /// Registers one channel wrapped in the full decorator chain.
    ///
    /// Order, from the outside in:
    ///   RateLimiting -> Retry -> Logging -> the channel itself
    ///
    /// Rate limiting is outermost so a rejected send never consumes retries.
    /// Logging is innermost so every individual attempt is recorded, including
    /// the ones that fail and get retried.
    /// </summary>
    private static void AddDecoratedChannel(
        IServiceCollection services,
        NotificationChannel key,
        Func<IServiceProvider, INotificationChannel> createChannel)
    {
        // Singleton: both the rate limiter's window and the Slack channel's
        // attempt counter are state that must persist across sends.
        services.AddKeyedSingleton<INotificationChannel>(key, (provider, _) =>
        {
            var settings = provider.GetRequiredService<IOptions<NotificationSettings>>();
            var logger = provider.GetRequiredService<INotificationLogger>();
            var timeProvider = provider.GetRequiredService<TimeProvider>();

            INotificationChannel channel = createChannel(provider);
            channel = new LoggingChannelDecorator(channel, logger);
            channel = new RetryChannelDecorator(channel, settings);
            channel = new RateLimitingChannelDecorator(channel, settings, timeProvider);
            return channel;
        });
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
