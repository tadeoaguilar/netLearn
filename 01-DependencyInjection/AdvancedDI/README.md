# AdvancedDI - Advanced Dependency Injection Patterns

## Overview
Take your DI skills to the professional level. Learn the patterns and techniques used in enterprise production applications: factories, decorators, configuration binding, and more.

## What You'll Learn

### Advanced Patterns
- **Factory Pattern**: Create services dynamically at runtime
- **Decorator Pattern**: Add behavior layers without modifying original code
- **Options Pattern**: Bind configuration to strongly-typed classes
- **Keyed Services**: Register multiple implementations with identifiers
- **Conditional Registration**: Environment-based service registration
- **Manual Scopes**: Control service provider scopes explicitly

### Enterprise Skills
- Runtime strategy selection
- Cross-cutting concerns (logging, validation, caching)
- Type-safe configuration management
- Multi-implementation scenarios
- Environment-specific behavior
- Lifetime management in complex scenarios

## Why These Patterns Matter

### Real-World Scenarios

**Factory Pattern**:
```csharp
// User selects payment method at checkout
var processor = _factory.GetProcessor(user.PreferredPaymentMethod);
processor.ProcessPayment(order.Total);
```

**Decorator Pattern**:
```csharp
// Add logging, validation, caching without changing original service
IOrderService → LoggingDecorator → ValidationDecorator → OrderService
```

**Options Pattern**:
```csharp
// Type-safe access to configuration
public class EmailService
{
    public EmailService(IOptions<EmailSettings> options)
    {
        var smtpServer = options.Value.SmtpServer; // Strongly typed!
    }
}
```

## Project Structure

```
AdvancedDI/
├── AdvancedDI/
│   ├── AdvancedDI.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Services/
│   │   ├── PaymentProcessors.cs
│   │   ├── PaymentProcessorFactory.cs
│   │   ├── OrderService.cs
│   │   ├── OrderServiceDecorators.cs
│   │   ├── NotificationServices.cs
│   │   ├── ConditionalServices.cs
│   │   └── ScopedOperation.cs
│   └── Configuration/
│       └── AppSettings.cs
├── EXERCISE.md
├── GETTING_STARTED.md
└── README.md
```

## Quick Start

1. **Navigate:**
   ```bash
   cd AdvancedDI/AdvancedDI
   ```

2. **Verify:**
   ```bash
   dotnet build
   ```

3. **Start learning:**
   Open [EXERCISE.md](EXERCISE.md)

## The Learning Path

### Part 1: Factory Pattern (30 min)
Learn to create services based on runtime conditions

**Use Case**: Payment processing where user selects method at checkout

### Part 2: Decorator Pattern (35 min)
Add behavior layers (logging, caching, validation) without modifying code

**Use Case**: Adding logging and validation to an order service

### Part 3: Configuration Binding (25 min)
Bind JSON configuration to strongly-typed classes

**Use Case**: Database connection strings, API keys, feature flags

### Part 4: Named/Keyed Services (20 min)
Register multiple implementations of same interface

**Use Case**: Multiple notification channels (email, SMS, push)

### Part 5: Conditional Registration (20 min)
Register different services based on environment

**Use Case**: In-memory cache for dev, Redis for production

### Part 6: Service Provider Scopes (20 min)
Manually manage service lifetimes and scopes

**Use Case**: Background jobs creating their own scopes for DbContext

### Part 7: Challenge (60+ min)
Build a complete multi-tenant notification system

**Total Time**: 3-4 hours

## Pattern Deep Dive

### 1. Factory Pattern

**Problem**: Need to select implementation at runtime
```csharp
// Can't do this - which implementation?
builder.Services.AddTransient<IPaymentProcessor, ???>();
```

**Solution**: Factory creates the right instance
```csharp
public interface IPaymentProcessorFactory
{
    IPaymentProcessor GetProcessor(PaymentMethod method);
}

// Usage
var processor = _factory.GetProcessor(user.SelectedMethod);
```

**When to Use**:
- User/runtime determines implementation
- Complex creation logic
- Need different instances for different contexts

---

### 2. Decorator Pattern

**Problem**: Want to add logging/caching/validation without changing original class

**Solution**: Wrap the service in decorator layers
```csharp
public class LoggingOrderService : IOrderService
{
    private readonly IOrderService _inner;

    public void PlaceOrder(Order order)
    {
        Console.WriteLine("Logging...");
        _inner.PlaceOrder(order);
        Console.WriteLine("Logged!");
    }
}
```

**Chain**:
```
Client → Logging → Validation → Caching → RealService
```

**When to Use**:
- Cross-cutting concerns
- Need to add behavior without modification
- Want to combine multiple behaviors
- Testing (can mock inner service)

---

### 3. Options Pattern

**Problem**: Configuration scattered, not type-safe

**Solution**: Bind to POCOs
```csharp
// appsettings.json
{
  "Email": {
    "SmtpServer": "smtp.example.com",
    "Port": 587
  }
}

// Configuration class
public class EmailSettings
{
    public string SmtpServer { get; set; }
    public int Port { get; set; }
}

// Registration
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));

// Usage
public class EmailService
{
    public EmailService(IOptions<EmailSettings> options)
    {
        var settings = options.Value; // Type-safe!
    }
}
```

**Variants**:
- `IOptions<T>`: Singleton snapshot
- `IOptionsSnapshot<T>`: Scoped snapshot (reloads per request)
- `IOptionsMonitor<T>`: Singleton with change notifications

**When to Use**:
- All configuration scenarios
- Type safety required
- Need validation
- Want hot-reload capability

---

### 4. Keyed Services (.NET 8+)

**Problem**: Multiple implementations of same interface

**Solution**: Register with keys
```csharp
builder.Services.AddKeyedTransient<INotificationService, EmailService>("email");
builder.Services.AddKeyedTransient<INotificationService, SmsService>("sms");

// Resolve by key
var emailer = provider.GetRequiredKeyedService<INotificationService>("email");
```

**Alternative (pre-.NET 8)**:
Use resolver/factory pattern

**When to Use**:
- Multiple implementations needed simultaneously
- Selection by string key makes sense
- .NET 8+ project

---

### 5. Conditional Registration

**Problem**: Different services for different environments

**Solution**: Register based on conditions
```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ICache, InMemoryCache>();
}
else
{
    builder.Services.AddSingleton<ICache, RedisCache>();
}
```

**When to Use**:
- Dev/staging/prod differences
- Feature flags
- A/B testing
- Tenant-specific services

---

### 6. Manual Scopes

**Problem**: Need custom lifetime control (e.g., background jobs)

**Solution**: Create scopes manually
```csharp
using (var scope = _serviceProvider.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
    // Use dbContext...
} // Disposed here
```

**When to Use**:
- Background services
- Message queue processors
- Long-running operations
- Custom lifetime management

## Common Patterns Combination

### Example: E-Commerce Order Processing

```csharp
// Configuration
builder.Services.Configure<PaymentSettings>(config.GetSection("Payment"));
builder.Services.Configure<ShippingSettings>(config.GetSection("Shipping"));

// Factories
builder.Services.AddSingleton<IPaymentProcessorFactory, PaymentProcessorFactory>();
builder.Services.AddSingleton<IShippingProviderFactory, ShippingProviderFactory>();

// Decorators
builder.Services.AddTransient<OrderService>();
builder.Services.AddTransient<IOrderService>(provider =>
{
    var baseService = provider.GetRequiredService<OrderService>();
    var withCache = new CachingOrderServiceDecorator(baseService);
    var withValidation = new ValidationOrderServiceDecorator(withCache);
    var withLogging = new LoggingOrderServiceDecorator(withValidation);
    return withLogging;
});

// Conditional
if (environment.IsDevelopment())
{
    builder.Services.AddSingleton<ICache, InMemoryCache>();
}
else
{
    builder.Services.AddSingleton<ICache, RedisCache>();
}
```

## Best Practices

### Factory Pattern
✅ Use for runtime selection
✅ Keep factory interface simple
✅ Let factory manage dependencies
❌ Don't create objects with `new` in business logic

### Decorator Pattern
✅ Keep decorators focused (single responsibility)
✅ Order matters (logging → validation → service)
✅ All decorators implement same interface
❌ Don't create deep chains (3-4 max)

### Options Pattern
✅ Always use IOptions/IOptionsSnapshot
✅ Validate configuration on startup
✅ Use sections for organization
❌ Don't inject IConfiguration directly into services

### Keyed Services
✅ Use descriptive keys
✅ Consider factory pattern as alternative
✅ Document available keys
❌ Don't overuse (factory might be better)

### Conditional Registration
✅ Make conditions explicit
✅ Document environment differences
✅ Test all paths
❌ Don't create too many branches

## Testing Considerations

### Factories
```csharp
// Easy to mock
var mockFactory = new Mock<IPaymentProcessorFactory>();
mockFactory.Setup(f => f.GetProcessor(PaymentMethod.Card))
           .Returns(mockProcessor);
```

### Decorators
```csharp
// Test inner service without decorators
var service = new OrderService();

// Test decorator in isolation
var decorator = new LoggingOrderServiceDecorator(mockInner);
```

### Options
```csharp
// Inject fake options
var options = Options.Create(new EmailSettings
{
    SmtpServer = "test.smtp.com"
});

var service = new EmailService(options);
```

## Next Steps

After completing AdvancedDI:

1. **Review all three DI modules**
   - BasicDI: Fundamentals
   - DILifetimes: Transient, Scoped, Singleton
   - AdvancedDI: Patterns

2. **Apply to real projects**
   - Identify where factories would help
   - Add decorators for cross-cutting concerns
   - Convert to Options pattern

3. **Move to Module 2**
   - [02-AsynchronousProcessing](../../02-AsynchronousProcessing/)
   - Learn async/await patterns
   - Build high-performance applications

## Checklist

After this module, you should be able to:

- [ ] Implement factory pattern for runtime selection
- [ ] Create decorator chains for cross-cutting concerns
- [ ] Bind configuration using Options pattern
- [ ] Use keyed services or service resolvers
- [ ] Register services conditionally
- [ ] Create and manage service provider scopes
- [ ] Combine multiple patterns effectively
- [ ] Choose the right pattern for the scenario

---

**Ready to master advanced DI?** Open [EXERCISE.md](EXERCISE.md)! 🚀
