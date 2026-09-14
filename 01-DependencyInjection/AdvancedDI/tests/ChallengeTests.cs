using AdvancedDI.Challenge;
using AdvancedDI.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AdvancedDI.Tests;

/// <summary>
/// Part 7: the multi-tenant notification system. These are the tests that make
/// the challenge checkable -- retry, rate limiting and tenant isolation are all
/// behaviours you cannot confirm by reading console output.
/// </summary>
public class ChallengeTests
{
    private sealed class CountingChannel : INotificationChannel
    {
        private readonly int _failuresBeforeSuccess;

        public CountingChannel(int failuresBeforeSuccess = 0)
            => _failuresBeforeSuccess = failuresBeforeSuccess;

        public string Name => "Counting";
        public int Attempts { get; private set; }
        public List<string> Delivered { get; } = new();

        public void Send(string tenantId, string message)
        {
            Attempts++;
            if (Attempts <= _failuresBeforeSuccess)
            {
                throw new InvalidOperationException($"transient failure {Attempts}");
            }

            Delivered.Add($"{tenantId}:{message}");
        }
    }

    [Fact]
    public void Retry_recovers_from_transient_failures()
    {
        var channel = new CountingChannel(failuresBeforeSuccess: 2);
        var sut = new RetryChannelDecorator(channel, maxRetries: 3);

        sut.Send("acme", "hello");

        channel.Attempts.Should().Be(3);
        channel.Delivered.Should().ContainSingle().Which.Should().Be("acme:hello");
    }

    [Fact]
    public void Retry_gives_up_once_the_budget_is_spent()
    {
        var channel = new CountingChannel(failuresBeforeSuccess: 10);
        var sut = new RetryChannelDecorator(channel, maxRetries: 2);

        var act = () => sut.Send("acme", "hello");

        act.Should().Throw<InvalidOperationException>();
        channel.Attempts.Should().Be(3, "the initial attempt plus two retries");
    }

    [Fact]
    public void Rate_limiting_allows_the_configured_number_then_rejects()
    {
        var time = new FakeTimeProvider();
        var channel = new CountingChannel();
        var sut = new RateLimitingChannelDecorator(channel, maxPerMinute: 3, time);

        sut.Send("acme", "1");
        sut.Send("acme", "2");
        sut.Send("acme", "3");
        var fourth = () => sut.Send("acme", "4");

        fourth.Should().Throw<RateLimitExceededException>();
        channel.Delivered.Should().HaveCount(3);
    }

    [Fact]
    public void The_rate_limit_window_slides_forward_in_time()
    {
        // A fake clock is what makes this testable -- the alternative is a test
        // that sleeps for a minute.
        var time = new FakeTimeProvider();
        var channel = new CountingChannel();
        var sut = new RateLimitingChannelDecorator(channel, maxPerMinute: 2, time);

        sut.Send("acme", "1");
        sut.Send("acme", "2");

        time.Advance(TimeSpan.FromSeconds(61));
        sut.Send("acme", "3");

        channel.Delivered.Should().HaveCount(3);
    }

    [Fact]
    public void A_rate_limited_send_never_consumes_the_retry_budget()
    {
        // Ordering check: rate limiting sits outside retry, so a rejection is
        // not mistaken for a transient failure and hammered three more times.
        var time = new FakeTimeProvider();
        var channel = new CountingChannel();
        INotificationChannel chain = new RetryChannelDecorator(channel, maxRetries: 3);
        chain = new RateLimitingChannelDecorator(chain, maxPerMinute: 1, time);

        chain.Send("acme", "first");
        var second = () => chain.Send("acme", "second");

        second.Should().Throw<RateLimitExceededException>();
        channel.Attempts.Should().Be(1, "the rejected send never reached the channel");
    }

    [Fact]
    public void The_logger_records_every_attempt_including_failures()
    {
        var logger = new InMemoryNotificationLogger();
        var channel = new CountingChannel(failuresBeforeSuccess: 1);
        INotificationChannel chain = new LoggingChannelDecorator(channel, logger);
        chain = new RetryChannelDecorator(chain, maxRetries: 2);

        chain.Send("acme", "deploy");

        logger.History.Should().Equal(
            "acme|Counting|deploy|FAILED",
            "acme|Counting|deploy|OK");
    }

    [Fact]
    public void Each_scope_carries_its_own_tenant()
    {
        using var provider = BuildHost();

        using var acme = provider.CreateScope();
        using var globex = provider.CreateScope();

        var acmeService = ForTenant(acme.ServiceProvider, "acme-corp");
        var globexService = ForTenant(globex.ServiceProvider, "globex");

        acmeService.Notify(NotificationChannel.Email, "invoice");
        globexService.Notify(NotificationChannel.Email, "invoice");

        var logger = provider.GetRequiredService<INotificationLogger>();
        logger.History.Should().Equal(
            "acme-corp|Email|invoice|OK",
            "globex|Email|invoice|OK");
    }

    [Fact]
    public void The_audit_log_is_a_singleton_shared_across_scopes()
    {
        using var provider = BuildHost();

        using (var scope = provider.CreateScope())
        {
            ForTenant(scope.ServiceProvider, "acme-corp")
                .Notify(NotificationChannel.Sms, "one");
        }

        using (var scope = provider.CreateScope())
        {
            ForTenant(scope.ServiceProvider, "globex")
                .Notify(NotificationChannel.Sms, "two");
        }

        provider.GetRequiredService<INotificationLogger>()
            .History.Should().HaveCount(2, "the log outlives any single scope");
    }

    [Fact]
    public void The_factory_returns_a_distinct_channel_per_key()
    {
        using var provider = BuildHost();
        var factory = provider.GetRequiredService<INotificationFactory>();

        factory.GetChannel(NotificationChannel.Email).Name.Should().Be("Email");
        factory.GetChannel(NotificationChannel.Sms).Name.Should().Be("SMS");
        factory.GetChannel(NotificationChannel.Push).Name.Should().Be("Push");
        factory.GetChannel(NotificationChannel.Slack).Name.Should().Be("Slack");
    }

    private static ServiceProvider BuildHost()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:MaxPerMinute"] = "100",
                ["Notifications:MaxRetries"] = "3"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddNotifications(configuration);
        return services.BuildServiceProvider();
    }

    private static NotificationService ForTenant(IServiceProvider provider, string tenantId)
        => new(new TenantContext(tenantId), provider.GetRequiredService<INotificationFactory>());
}
