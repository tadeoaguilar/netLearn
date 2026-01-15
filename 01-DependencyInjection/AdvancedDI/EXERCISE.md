# Exercise: Advanced Dependency Injection Patterns

## Overview
Now that you understand DI basics and lifetimes, it's time to learn professional patterns used in production applications. This module covers factory patterns, decorators, configuration binding, and other advanced techniques.

## Learning Goals
- Implement factory patterns for runtime strategy selection
- Use the decorator pattern for cross-cutting concerns
- Bind configuration to strongly-typed objects
- Work with named/keyed services
- Handle conditional service registration
- Create and manage service provider scopes
- Use open generic types

---

## Part 1: Factory Pattern with DI

### Why Factories?
Sometimes you need to create instances at runtime based on conditions. Factories solve this while maintaining DI principles.

### Step 1.1: The Problem - Runtime Selection

**Your Task:**
Create `Services/PaymentProcessors.cs`:

```csharp
namespace AdvancedDI.Services;

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
```

**The Challenge:**
How do you choose which processor to use at runtime based on user selection?

### Step 1.2: Solution - Factory Pattern

**Your Task:**
Create `Services/PaymentProcessorFactory.cs`:

```csharp
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
```

### Step 1.3: Using the Factory

Update `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AdvancedDI.Services;

Console.WriteLine("=== FACTORY PATTERN ===\n");

var builder = Host.CreateApplicationBuilder(args);

// Register all payment processors
builder.Services.AddTransient<CreditCardProcessor>();
builder.Services.AddTransient<PayPalProcessor>();
builder.Services.AddTransient<CryptoProcessor>();

// Register the factory
builder.Services.AddSingleton<IPaymentProcessorFactory, PaymentProcessorFactory>();

var host = builder.Build();

var factory = host.Services.GetRequiredService<IPaymentProcessorFactory>();

// User selects payment method at runtime
var userChoice = PaymentMethod.PayPal;
var processor = factory.GetProcessor(userChoice);
processor.ProcessPayment(99.99m);

// Different choice
userChoice = PaymentMethod.Crypto;
processor = factory.GetProcessor(userChoice);
processor.ProcessPayment(50.00m);
```

**Run it:**
```bash
dotnet run
```

**Key Benefits:**
- Runtime selection without breaking DI
- All processors still benefit from DI
- Easy to add new processors
- Testable (can mock factory)

---

## Part 2: Decorator Pattern

### What is the Decorator Pattern?
Wrap services with additional behavior (logging, caching, validation) without modifying the original service.

### Step 2.1: Base Service

**Your Task:**
Create `Services/OrderService.cs`:

```csharp
namespace AdvancedDI.Services;

public interface IOrderService
{
    void PlaceOrder(string productName, int quantity);
}

public class OrderService : IOrderService
{
    public void PlaceOrder(string productName, int quantity)
    {
        Console.WriteLine($"[OrderService] Placing order: {quantity}x {productName}");
    }
}
```

### Step 2.2: Create Decorators

**Your Task:**
Create `Services/OrderServiceDecorators.cs`:

```csharp
namespace AdvancedDI.Services;

// Decorator 1: Logging
public class LoggingOrderServiceDecorator : IOrderService
{
    private readonly IOrderService _inner;

    public LoggingOrderServiceDecorator(IOrderService inner)
    {
        _inner = inner;
    }

    public void PlaceOrder(string productName, int quantity)
    {
        Console.WriteLine($"[LOGGING] Order request received at {DateTime.Now:HH:mm:ss}");
        _inner.PlaceOrder(productName, quantity);
        Console.WriteLine($"[LOGGING] Order request completed");
    }
}

// Decorator 2: Validation
public class ValidationOrderServiceDecorator : IOrderService
{
    private readonly IOrderService _inner;

    public ValidationOrderServiceDecorator(IOrderService inner)
    {
        _inner = inner;
    }

    public void PlaceOrder(string productName, int quantity)
    {
        Console.WriteLine($"[VALIDATION] Validating order...");

        if (string.IsNullOrWhiteSpace(productName))
        {
            Console.WriteLine($"[VALIDATION] FAILED - Product name is required");
            return;
        }

        if (quantity <= 0)
        {
            Console.WriteLine($"[VALIDATION] FAILED - Quantity must be positive");
            return;
        }

        Console.WriteLine($"[VALIDATION] PASSED");
        _inner.PlaceOrder(productName, quantity);
    }
}

// Decorator 3: Caching (simulated)
public class CachingOrderServiceDecorator : IOrderService
{
    private readonly IOrderService _inner;
    private readonly HashSet<string> _recentOrders = new();

    public CachingOrderServiceDecorator(IOrderService inner)
    {
        _inner = inner;
    }

    public void PlaceOrder(string productName, int quantity)
    {
        var key = $"{productName}-{quantity}";

        if (_recentOrders.Contains(key))
        {
            Console.WriteLine($"[CACHING] Duplicate order detected for {productName}");
        }
        else
        {
            _recentOrders.Add(key);
        }

        _inner.PlaceOrder(productName, quantity);
    }
}
```

### Step 2.3: Chaining Decorators

Update `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AdvancedDI.Services;

Console.WriteLine("=== DECORATOR PATTERN ===\n");

var services = new ServiceCollection();

// Register base service
services.AddTransient<OrderService>();

// Manual decorator chain:
// User -> Logging -> Validation -> Caching -> OrderService
services.AddTransient<IOrderService>(provider =>
{
    var baseService = provider.GetRequiredService<OrderService>();
    var withCaching = new CachingOrderServiceDecorator(baseService);
    var withValidation = new ValidationOrderServiceDecorator(withCaching);
    var withLogging = new LoggingOrderServiceDecorator(withValidation);
    return withLogging;
});

var provider = services.BuildServiceProvider();

var orderService = provider.GetRequiredService<IOrderService>();

Console.WriteLine("Order 1:");
orderService.PlaceOrder("Laptop", 2);

Console.WriteLine("\nOrder 2 (duplicate):");
orderService.PlaceOrder("Laptop", 2);

Console.WriteLine("\nOrder 3 (invalid):");
orderService.PlaceOrder("", 5);
```

**Run it:**
```bash
dotnet run
```

**Observe the chain:**
1. Logging (enters)
2. Validation (checks)
3. Caching (checks duplicates)
4. OrderService (actual work)
5. Logging (exits)

---

## Part 3: Configuration Binding (Options Pattern)

### Step 3.1: Create Configuration Classes

**Your Task:**
Create `Configuration/AppSettings.cs`:

```csharp
namespace AdvancedDI.Configuration;

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public int MaxRetries { get; set; }
    public int TimeoutSeconds { get; set; }
}

public class EmailSettings
{
    public string SmtpServer { get; set; } = string.Empty;
    public int Port { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
}

public class AppSettings
{
    public string ApplicationName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public DatabaseSettings Database { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
}
```

### Step 3.2: Create appsettings.json

**Your Task:**
Create `appsettings.json` in the project root:

```json
{
  "ApplicationName": "AdvancedDI Demo",
  "Environment": "Development",
  "Database": {
    "ConnectionString": "Server=localhost;Database=MyApp;",
    "MaxRetries": 3,
    "TimeoutSeconds": 30
  },
  "Email": {
    "SmtpServer": "smtp.example.com",
    "Port": 587,
    "FromAddress": "noreply@example.com",
    "UseSsl": true
  }
}
```

Don't forget to mark it as "Copy to Output Directory" in the .csproj:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### Step 3.3: Bind Configuration

**Your Task:**
Create `Services/ConfigurationConsumer.cs`:

```csharp
using Microsoft.Extensions.Options;
using AdvancedDI.Configuration;

namespace AdvancedDI.Services;

public class ConfigurationConsumer
{
    private readonly AppSettings _appSettings;
    private readonly DatabaseSettings _dbSettings;
    private readonly EmailSettings _emailSettings;

    public ConfigurationConsumer(
        IOptions<AppSettings> appSettings,
        IOptions<DatabaseSettings> dbSettings,
        IOptions<EmailSettings> emailSettings)
    {
        _appSettings = appSettings.Value;
        _dbSettings = dbSettings.Value;
        _emailSettings = emailSettings.Value;
    }

    public void DisplayConfiguration()
    {
        Console.WriteLine("=== APPLICATION CONFIGURATION ===\n");

        Console.WriteLine($"App Name: {_appSettings.ApplicationName}");
        Console.WriteLine($"Environment: {_appSettings.Environment}\n");

        Console.WriteLine("Database Settings:");
        Console.WriteLine($"  Connection: {_dbSettings.ConnectionString}");
        Console.WriteLine($"  Max Retries: {_dbSettings.MaxRetries}");
        Console.WriteLine($"  Timeout: {_dbSettings.TimeoutSeconds}s\n");

        Console.WriteLine("Email Settings:");
        Console.WriteLine($"  SMTP Server: {_emailSettings.SmtpServer}");
        Console.WriteLine($"  Port: {_emailSettings.Port}");
        Console.WriteLine($"  From: {_emailSettings.FromAddress}");
        Console.WriteLine($"  SSL: {_emailSettings.UseSsl}");
    }
}
```

Update `Program.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AdvancedDI.Configuration;
using AdvancedDI.Services;

Console.WriteLine("=== CONFIGURATION BINDING ===\n");

var builder = Host.CreateApplicationBuilder(args);

// Load configuration from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false);

// Bind entire configuration
builder.Services.Configure<AppSettings>(builder.Configuration);

// Bind specific sections
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("Database"));

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));

builder.Services.AddTransient<ConfigurationConsumer>();

var host = builder.Build();

var consumer = host.Services.GetRequiredService<ConfigurationConsumer>();
consumer.DisplayConfiguration();
```

**Run it:**
```bash
dotnet run
```

**Benefits:**
- Type-safe configuration
- Easy to test (inject fake IOptions)
- Configuration validation possible
- Change detection with IOptionsMonitor

---

## Part 4: Named/Keyed Services

### Step 4.1: Multiple Implementations

Sometimes you need multiple implementations of the same interface registered simultaneously.

**Your Task:**
Create `Services/NotificationServices.cs`:

```csharp
namespace AdvancedDI.Services;

public interface INotificationService
{
    void Send(string message);
}

public class EmailNotificationService : INotificationService
{
    public void Send(string message)
    {
        Console.WriteLine($"[EMAIL] Sending: {message}");
    }
}

public class SmsNotificationService : INotificationService
{
    public void Send(string message)
    {
        Console.WriteLine($"[SMS] Sending: {message}");
    }
}

public class PushNotificationService : INotificationService
{
    public void Send(string message)
    {
        Console.WriteLine($"[PUSH] Sending: {message}");
    }
}
```

### Step 4.2: Keyed Services (.NET 8+)

Update `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AdvancedDI.Services;

Console.WriteLine("=== KEYED SERVICES ===\n");

var builder = Host.CreateApplicationBuilder(args);

// Register services with keys
builder.Services.AddKeyedTransient<INotificationService, EmailNotificationService>("email");
builder.Services.AddKeyedTransient<INotificationService, SmsNotificationService>("sms");
builder.Services.AddKeyedTransient<INotificationService, PushNotificationService>("push");

var host = builder.Build();

// Resolve by key
var emailService = host.Services.GetRequiredKeyedService<INotificationService>("email");
var smsService = host.Services.GetRequiredKeyedService<INotificationService>("sms");
var pushService = host.Services.GetRequiredKeyedService<INotificationService>("push");

emailService.Send("Welcome to our platform!");
smsService.Send("Your code is 1234");
pushService.Send("New message received");
```

### Step 4.3: Alternative - Service Resolver Pattern

For pre-.NET 8 or more complex scenarios:

```csharp
namespace AdvancedDI.Services;

public class NotificationServiceResolver
{
    private readonly IServiceProvider _serviceProvider;

    public NotificationServiceResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public INotificationService GetService(string type)
    {
        return type.ToLower() switch
        {
            "email" => _serviceProvider.GetRequiredService<EmailNotificationService>(),
            "sms" => _serviceProvider.GetRequiredService<SmsNotificationService>(),
            "push" => _serviceProvider.GetRequiredService<PushNotificationService>(),
            _ => throw new ArgumentException($"Unknown notification type: {type}")
        };
    }
}
```

---

## Part 5: Conditional Registration

### Step 5.1: Environment-Based Registration

**Your Task:**
Create `Services/ConditionalServices.cs`:

```csharp
namespace AdvancedDI.Services;

public interface ICacheService
{
    void Set(string key, string value);
    string? Get(string key);
}

public class RedisCacheService : ICacheService
{
    private readonly Dictionary<string, string> _cache = new();

    public RedisCacheService()
    {
        Console.WriteLine("[REDIS] Redis cache service initialized (production)");
    }

    public void Set(string key, string value)
    {
        _cache[key] = value;
        Console.WriteLine($"[REDIS] Set {key} = {value}");
    }

    public string? Get(string key)
    {
        _cache.TryGetValue(key, out var value);
        Console.WriteLine($"[REDIS] Get {key} = {value ?? "null"}");
        return value;
    }
}

public class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, string> _cache = new();

    public InMemoryCacheService()
    {
        Console.WriteLine("[MEMORY] In-memory cache service initialized (development)");
    }

    public void Set(string key, string value)
    {
        _cache[key] = value;
        Console.WriteLine($"[MEMORY] Set {key} = {value}");
    }

    public string? Get(string key)
    {
        _cache.TryGetValue(key, out var value);
        Console.WriteLine($"[MEMORY] Get {key} = {value ?? "null"}");
        return value;
    }
}
```

Update `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AdvancedDI.Services;

Console.WriteLine("=== CONDITIONAL REGISTRATION ===\n");

var builder = Host.CreateApplicationBuilder(args);

// Get environment
var environment = builder.Environment.EnvironmentName;
Console.WriteLine($"Environment: {environment}\n");

// Register based on environment
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
}
else
{
    builder.Services.AddSingleton<ICacheService, RedisCacheService>();
}

var host = builder.Build();

var cache = host.Services.GetRequiredService<ICacheService>();
cache.Set("user:1", "John Doe");
var value = cache.Get("user:1");
```

**Run in different environments:**
```bash
# Development
dotnet run

# Production
dotnet run --environment Production
```

---

## Part 6: Service Provider Scopes

### Step 6.1: Managing Scopes Manually

**Your Task:**
Create `Services/ScopedOperation.cs`:

```csharp
namespace AdvancedDI.Services;

public interface IDatabaseConnection
{
    Guid ConnectionId { get; }
    void ExecuteQuery(string query);
}

public class DatabaseConnection : IDatabaseConnection, IDisposable
{
    public Guid ConnectionId { get; }

    public DatabaseConnection()
    {
        ConnectionId = Guid.NewGuid();
        Console.WriteLine($"[DB] Connection opened: {ConnectionId}");
    }

    public void ExecuteQuery(string query)
    {
        Console.WriteLine($"[DB] Executing on {ConnectionId}: {query}");
    }

    public void Dispose()
    {
        Console.WriteLine($"[DB] Connection closed: {ConnectionId}");
    }
}

public class UnitOfWork
{
    private readonly IDatabaseConnection _connection;

    public UnitOfWork(IDatabaseConnection connection)
    {
        _connection = connection;
    }

    public void DoWork(string operation)
    {
        Console.WriteLine($"\n[UnitOfWork] Starting: {operation}");
        _connection.ExecuteQuery($"BEGIN TRANSACTION");
        _connection.ExecuteQuery($"-- {operation} --");
        _connection.ExecuteQuery($"COMMIT");
        Console.WriteLine($"[UnitOfWork] Completed: {operation}");
    }
}
```

Update `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AdvancedDI.Services;

Console.WriteLine("=== SERVICE PROVIDER SCOPES ===\n");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<IDatabaseConnection, DatabaseConnection>();
builder.Services.AddTransient<UnitOfWork>();

var host = builder.Build();

Console.WriteLine("Creating Scope 1:");
using (var scope1 = host.Services.CreateScope())
{
    var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<UnitOfWork>();
    unitOfWork1.DoWork("Create User");

    var unitOfWork2 = scope1.ServiceProvider.GetRequiredService<UnitOfWork>();
    unitOfWork2.DoWork("Update Profile");
    // Same connection for both operations
}

Console.WriteLine("\nCreating Scope 2:");
using (var scope2 = host.Services.CreateScope())
{
    var unitOfWork3 = scope2.ServiceProvider.GetRequiredService<UnitOfWork>();
    unitOfWork3.DoWork("Delete Record");
    // New connection
}

Console.WriteLine("\nDone!");
```

**Observe:**
- Scope 1: Same connection for both operations
- Scope 2: New connection
- Connections disposed when scope ends

---

## Part 7: Challenge Exercise

### Challenge: Build a Multi-Tenant Notification System

**Requirements:**

1. **Multiple Notification Channels**:
   - Email, SMS, Push, Slack
   - Each implemented as `INotificationService`

2. **Factory Pattern**:
   - `INotificationFactory` to get channels by type
   - Support runtime selection

3. **Decorator Pattern**:
   - Logging decorator (logs all notifications)
   - Retry decorator (retries failed notifications)
   - Rate limiting decorator (limits notifications per minute)

4. **Configuration**:
   - Load notification settings from `appsettings.json`
   - Email SMTP settings
   - SMS API keys
   - Rate limits

5. **Tenant Resolver** (Scoped):
   - Each request has a tenant context
   - Notifications are sent on behalf of tenant

6. **Service**:
   - `NotificationService` that:
     - Gets current tenant
     - Resolves notification channel
     - Applies decorators
     - Sends notification

**Bonus**:
- Add a `INotificationLogger` (Singleton) that tracks all notifications
- Implement `IOptionsSnapshot` for hot-reload of configuration
- Add validation for configuration on startup

---

## Reflection Questions

1. **When would you use a factory instead of direct DI?**

2. **What's the benefit of the decorator pattern over modifying the original class?**

3. **Why use IOptions instead of injecting IConfiguration directly?**

4. **How do keyed services differ from factories?**

5. **When would you create a scope manually instead of relying on framework scopes?**

---

## Summary

You've learned:
- ✅ **Factory Pattern**: Runtime service selection
- ✅ **Decorator Pattern**: Layering behavior without modification
- ✅ **Options Pattern**: Type-safe configuration binding
- ✅ **Keyed Services**: Multiple implementations with keys
- ✅ **Conditional Registration**: Environment-based setup
- ✅ **Manual Scopes**: Control over service lifetimes

## Key Patterns Comparison

| Pattern | Use When | Example |
|---------|----------|---------|
| **Factory** | Runtime selection needed | Payment processor based on user choice |
| **Decorator** | Add behavior to existing service | Logging, caching, validation layers |
| **Options** | Type-safe configuration | Database settings, API keys |
| **Keyed** | Multiple implementations | Different notification channels |
| **Conditional** | Environment-specific services | Dev vs Prod cache |

## Next Steps

You've completed the Dependency Injection module! Next:

1. **Review**: Go back through exercises and ensure you understand each pattern
2. **Practice**: Apply these patterns to your own projects
3. **Move On**: Start **[Module 2: Asynchronous Processing](../../02-AsynchronousProcessing/)**

---

**Congratulations on completing Advanced DI!** 🎉
