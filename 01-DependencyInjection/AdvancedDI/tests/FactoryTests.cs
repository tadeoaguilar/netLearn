using AdvancedDI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedDI.Tests;

/// <summary>Part 1: the factory selects the right implementation at runtime.</summary>
public class FactoryTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddTransient<CreditCardProcessor>();
        services.AddTransient<PayPalProcessor>();
        services.AddTransient<CryptoProcessor>();
        services.AddSingleton<IPaymentProcessorFactory, PaymentProcessorFactory>();
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(PaymentMethod.CreditCard, typeof(CreditCardProcessor))]
    [InlineData(PaymentMethod.PayPal, typeof(PayPalProcessor))]
    [InlineData(PaymentMethod.Crypto, typeof(CryptoProcessor))]
    public void GetProcessor_returns_the_implementation_matching_the_method(
        PaymentMethod method, Type expected)
    {
        using var provider = BuildProvider();
        var factory = provider.GetRequiredService<IPaymentProcessorFactory>();

        factory.GetProcessor(method).Should().BeOfType(expected);
    }

    [Fact]
    public void GetProcessor_rejects_an_unknown_method()
    {
        using var provider = BuildProvider();
        var factory = provider.GetRequiredService<IPaymentProcessorFactory>();

        var act = () => factory.GetProcessor((PaymentMethod)99);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_singleton_factory_still_hands_out_fresh_transient_processors()
    {
        // This is the property that makes a Singleton factory safe: it resolves
        // per call instead of caching, so it does not capture a Transient.
        using var provider = BuildProvider();
        var factory = provider.GetRequiredService<IPaymentProcessorFactory>();

        var first = factory.GetProcessor(PaymentMethod.PayPal);
        var second = factory.GetProcessor(PaymentMethod.PayPal);

        first.Should().NotBeSameAs(second);
    }
}
