using AdvancedDI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdvancedDI.Demos;

public static class Part1Factory
{
    public static void Run()
    {
        Console.WriteLine("=== PART 1: FACTORY PATTERN ===\n");

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddTransient<CreditCardProcessor>();
        builder.Services.AddTransient<PayPalProcessor>();
        builder.Services.AddTransient<CryptoProcessor>();
        builder.Services.AddSingleton<IPaymentProcessorFactory, PaymentProcessorFactory>();

        var host = builder.Build();
        var factory = host.Services.GetRequiredService<IPaymentProcessorFactory>();

        // The method is chosen at runtime -- something plain constructor
        // injection cannot express, because the container wires up graphs
        // before any of this code runs.
        factory.GetProcessor(PaymentMethod.PayPal).ProcessPayment(99.99m);
        factory.GetProcessor(PaymentMethod.Crypto).ProcessPayment(50.00m);
        factory.GetProcessor(PaymentMethod.CreditCard).ProcessPayment(12.30m);
    }
}
