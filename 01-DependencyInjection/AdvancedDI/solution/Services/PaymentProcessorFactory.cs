using Microsoft.Extensions.DependencyInjection;

namespace AdvancedDI.Services;

public enum PaymentMethod
{
    CreditCard,
    PayPal,
    Crypto
}

public interface IPaymentProcessorFactory
{
    IPaymentProcessor GetProcessor(PaymentMethod method);
}

/// <summary>
/// Resolves the processor through <see cref="IServiceProvider"/> rather than
/// calling <c>new</c>, so each processor keeps its own constructor injection.
/// </summary>
/// <remarks>
/// The factory is registered as a Singleton but the processors are Transient.
/// That is safe here because the factory resolves on each call instead of
/// caching an instance in a field -- caching one would be a captive dependency.
/// </remarks>
public class PaymentProcessorFactory : IPaymentProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PaymentProcessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPaymentProcessor GetProcessor(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.CreditCard => _serviceProvider.GetRequiredService<CreditCardProcessor>(),
            PaymentMethod.PayPal => _serviceProvider.GetRequiredService<PayPalProcessor>(),
            PaymentMethod.Crypto => _serviceProvider.GetRequiredService<CryptoProcessor>(),
            _ => throw new ArgumentException($"Unknown payment method: {method}")
        };
    }
}
