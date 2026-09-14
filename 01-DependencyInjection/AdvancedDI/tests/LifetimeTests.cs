using AdvancedDI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedDI.Tests;

/// <summary>
/// Part 6 plus the DILifetimes module: these tests pin down the lifetime rules
/// that the console demos only illustrate. If you change a registration in the
/// exercise and something here fails, the failure message tells you which rule
/// you broke.
/// </summary>
public class LifetimeTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDatabaseConnection, DatabaseConnection>();
        services.AddTransient<UnitOfWork>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Scoped_services_are_shared_within_one_scope()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<UnitOfWork>();
        var second = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

        first.Should().NotBeSameAs(second, "UnitOfWork itself is Transient");
        first.ConnectionId.Should().Be(second.ConnectionId,
            "but both share the one Scoped connection");
    }

    [Fact]
    public void Scoped_services_differ_between_scopes()
    {
        using var provider = BuildProvider();

        Guid firstScopeConnection;
        using (var scope = provider.CreateScope())
        {
            firstScopeConnection = scope.ServiceProvider
                .GetRequiredService<UnitOfWork>().ConnectionId;
        }

        using var otherScope = provider.CreateScope();
        var secondScopeConnection = otherScope.ServiceProvider
            .GetRequiredService<UnitOfWork>().ConnectionId;

        secondScopeConnection.Should().NotBe(firstScopeConnection);
    }

    [Fact]
    public void Disposable_scoped_services_are_disposed_when_the_scope_ends()
    {
        using var provider = BuildProvider();

        DatabaseConnection connection;
        using (var scope = provider.CreateScope())
        {
            connection = (DatabaseConnection)scope.ServiceProvider
                .GetRequiredService<IDatabaseConnection>();
        }

        // After disposal the container will not hand the instance out again,
        // and resolving from the disposed scope throws.
        connection.Should().NotBeNull();
    }

    [Fact]
    public void Resolving_a_scoped_service_from_the_root_provider_is_rejected()
    {
        // The default validation catches this at resolve time. Without it, a
        // "scoped" connection resolved from the root would silently live for
        // the whole application -- the classic captive dependency.
        var services = new ServiceCollection();
        services.AddScoped<IDatabaseConnection, DatabaseConnection>();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        var act = () => provider.GetRequiredService<IDatabaseConnection>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*scope*");
    }

    [Fact]
    public void Keyed_registrations_resolve_independently()
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient<INotificationService, EmailNotificationService>("email");
        services.AddKeyedTransient<INotificationService, SmsNotificationService>("sms");
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredKeyedService<INotificationService>("email")
            .Should().BeOfType<EmailNotificationService>();
        provider.GetRequiredKeyedService<INotificationService>("sms")
            .Should().BeOfType<SmsNotificationService>();

        // A keyed registration is NOT resolvable without its key.
        provider.GetService<INotificationService>().Should().BeNull();
    }
}
