namespace AdvancedDI.Services;

/// <summary>
/// Part 1: the family of implementations a factory will choose between at runtime.
/// </summary>
public interface IPaymentProcessor
{
    string ProcessorName { get; }
    void ProcessPayment(decimal amount);
}

public class CreditCardProcessor : IPaymentProcessor
{
    public string ProcessorName => "Credit Card";

    public void ProcessPayment(decimal amount)
    {
        Console.WriteLine($"[{ProcessorName}] Processing ${amount} via credit card");
    }
}

public class PayPalProcessor : IPaymentProcessor
{
    public string ProcessorName => "PayPal";

    public void ProcessPayment(decimal amount)
    {
        Console.WriteLine($"[{ProcessorName}] Processing ${amount} via PayPal");
    }
}

public class CryptoProcessor : IPaymentProcessor
{
    public string ProcessorName => "Cryptocurrency";

    public void ProcessPayment(decimal amount)
    {
        Console.WriteLine($"[{ProcessorName}] Processing ${amount} via crypto");
    }
}
