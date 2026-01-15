# Getting Started with AdvancedDI

## Quick Start

### 1. Navigate to Project
```bash
cd /Users/tadeo/gitlab/netLearn/01-DependencyInjection/AdvancedDI/AdvancedDI
```

### 2. Verify Setup
```bash
dotnet build
```

### 3. Project Structure

You'll create this as you work through exercises:

```
AdvancedDI/
├── AdvancedDI.csproj        # Project file (ready)
├── Program.cs               # You'll edit this for each part
├── appsettings.json         # Configuration file (Part 3)
├── Services/                # Create this folder
│   ├── PaymentProcessors.cs
│   ├── PaymentProcessorFactory.cs
│   ├── OrderService.cs
│   ├── OrderServiceDecorators.cs
│   ├── NotificationServices.cs
│   ├── ConditionalServices.cs
│   └── ScopedOperation.cs
└── Configuration/           # Create this folder
    └── AppSettings.cs
```

### 4. Create Folders
```bash
mkdir Services
mkdir Configuration
```

### 5. Start Exercising
Open [EXERCISE.md](EXERCISE.md) and begin with **Part 1: Factory Pattern**.

## What You've Learned So Far

### From BasicDI
- What DI is and why use it
- Constructor injection
- Interface-based design
- Using the DI container

### From DILifetimes
- Transient vs Scoped vs Singleton
- When to use each lifetime
- Captive dependencies (the big mistake!)

### Now: AdvancedDI
- **When** to use advanced patterns
- **How** to implement them professionally
- **Why** they solve real-world problems

## The Pattern Overview

### Quick Reference

| Pattern | Solves | Example |
|---------|--------|---------|
| **Factory** | Runtime selection | "Which payment processor?" |
| **Decorator** | Adding behavior | "Add logging to all orders" |
| **Options** | Configuration | "Load from appsettings.json" |
| **Keyed** | Multiple implementations | "Email vs SMS notification" |
| **Conditional** | Environment differences | "Dev cache vs Prod cache" |
| **Scopes** | Lifetime control | "Background job with DbContext" |

## How to Approach Each Part

### Part 1: Factory Pattern

**Goal**: Select service at runtime

**Key Concept**:
```csharp
// Instead of deciding at registration:
builder.Services.AddTransient<IPaymentProcessor, ???>();

// Decide at runtime:
var processor = factory.GetProcessor(userChoice);
```

**When You'll Use This**:
- User selects an option
- Configuration determines behavior
- Different tenants need different implementations

---

### Part 2: Decorator Pattern

**Goal**: Layer behavior without modifying original code

**Key Concept**:
```csharp
// Wrap services:
Client → Logging → Validation → RealService

// Each layer adds behavior
LoggingDecorator(ValidationDecorator(RealService))
```

**When You'll Use This**:
- Adding logging, caching, validation
- Cross-cutting concerns
- Don't want to modify original class

---

### Part 3: Options Pattern

**Goal**: Type-safe configuration

**Key Concept**:
```csharp
// Not this:
var server = configuration["Email:SmtpServer"]; // string, error-prone

// This:
var server = emailSettings.SmtpServer; // typed, safe
```

**When You'll Use This**:
- Always! For all configuration
- Want IntelliSense
- Need validation

---

### Part 4: Keyed Services

**Goal**: Multiple implementations with identifiers

**Key Concept**:
```csharp
// Register with keys
builder.Services.AddKeyedTransient<INotificationService, EmailService>("email");
builder.Services.AddKeyedTransient<INotificationService, SmsService>("sms");

// Resolve by key
var sms = provider.GetRequiredKeyedService<INotificationService>("sms");
```

**When You'll Use This**:
- Need all implementations simultaneously
- Selection by name/key makes sense
- .NET 8+ projects

---

### Part 5: Conditional Registration

**Goal**: Different services for different environments

**Key Concept**:
```csharp
if (isDevelopment)
    builder.Services.AddSingleton<ICache, InMemoryCache>();
else
    builder.Services.AddSingleton<ICache, RedisCache>();
```

**When You'll Use This**:
- Dev/staging/prod differences
- Feature flags
- Cost optimization (free tier in dev, premium in prod)

---

### Part 6: Manual Scopes

**Goal**: Control when services are created/disposed

**Key Concept**:
```csharp
// Create scope
using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DbContext>();
    // Use db
} // Disposed here
```

**When You'll Use This**:
- Background services
- Message queue consumers
- Long-running operations
- Custom lifetime requirements

## Tips for Success

### 1. Understand the "Why"
Don't just copy code. Understand:
- What problem does this solve?
- When would I need this?
- What happens without this pattern?

### 2. Run After Each Part
```bash
dotnet run
```
See the output. Understand what happened.

### 3. Modify and Experiment
After completing each part:
- Change the order (decorators)
- Add more implementations (factory)
- Try different configurations (options)

### 4. Connect to Real World
Think about your projects:
- Where would factories help?
- What cross-cutting concerns need decorators?
- What configuration needs Options pattern?

### 5. Don't Skip the Challenge
Part 7 combines everything. It's where learning solidifies.

## Common Patterns in Web APIs

### Typical Registration

```csharp
// Configuration
builder.Services.Configure<DatabaseSettings>(config.GetSection("Database"));
builder.Services.Configure<EmailSettings>(config.GetSection("Email"));

// Conditional
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailSender, FakeEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}

// Factory
builder.Services.AddSingleton<IPaymentProcessorFactory, PaymentProcessorFactory>();
builder.Services.AddTransient<CreditCardProcessor>();
builder.Services.AddTransient<PayPalProcessor>();

// Decorator
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<IOrderRepository>(provider =>
{
    var repo = provider.GetRequiredService<OrderRepository>();
    var withLogging = new LoggingOrderRepository(repo);
    var withCaching = new CachingOrderRepository(withLogging);
    return withCaching;
});
```

## What to Watch For

### Factory Pattern
Watch:
- How factory resolves from IServiceProvider
- Runtime selection based on input
- All implementations still use DI

### Decorator Pattern
Watch:
- Order of decorators (matters!)
- Each decorator wraps the previous
- Same interface throughout chain

### Options Pattern
Watch:
- Strongly-typed access
- No magic strings
- IntelliSense support

### Keyed Services
Watch:
- Multiple registrations of same interface
- Resolution by key
- .NET 8 feature

### Conditional Registration
Watch:
- Environment detection
- Different services registered
- Same interface, different implementation

### Manual Scopes
Watch:
- Scope creation
- Service resolution within scope
- Disposal at scope end

## Troubleshooting

### "Type or namespace not found"
- Create `Services` and `Configuration` folders
- Check namespace: `AdvancedDI.Services` or `AdvancedDI.Configuration`
- Add using statements

### "Cannot resolve service"
- Did you register the service?
- Check spelling of service registration
- For keyed services, check the key name

### "appsettings.json not found"
- Create file in project root
- Add to .csproj:
  ```xml
  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
  ```

### Decorator not working
- Check order (registration vs execution)
- Ensure all decorators implement same interface
- Verify inner service is passed correctly

## Key Concepts to Internalize

### 1. Composition Over Inheritance
Decorators compose behavior instead of inheriting.

### 2. Open/Closed Principle
Open for extension (decorators), closed for modification (original service).

### 3. Single Responsibility
Each decorator does one thing.

### 4. Dependency Inversion
Depend on abstractions (interfaces), not concretions.

### 5. Don't Repeat Yourself (DRY)
Factories centralize creation logic.

## After Completing This Module

You'll understand:
- When simple DI isn't enough
- Which pattern solves which problem
- How to implement patterns professionally
- How to combine patterns effectively

You'll be able to:
- Choose the right pattern for the scenario
- Implement factories for runtime selection
- Create decorator chains
- Use configuration properly
- Register services conditionally
- Manage scopes manually

## Real-World Application

### Before Advanced DI
```csharp
// Hard-coded, can't change
public class OrderService
{
    private readonly SqlRepository _repo = new SqlRepository();
    private readonly SmtpEmailer _emailer = new SmtpEmailer();

    public void PlaceOrder(Order order)
    {
        // No logging
        // No validation
        // No flexibility
        _repo.Save(order);
        _emailer.Send("Order placed");
    }
}
```

### After Advanced DI
```csharp
// Flexible, testable, extensible
public class OrderService : IOrderService
{
    private readonly IRepository _repo;
    private readonly IEmailSender _emailer;

    public OrderService(IRepository repo, IEmailSender emailer)
    {
        _repo = repo;
        _emailer = emailer;
    }

    public void PlaceOrder(Order order)
    {
        _repo.Save(order);
        _emailer.Send("Order placed");
    }
}

// Registration with decorators, factories, options
builder.Services.Configure<EmailSettings>(config.GetSection("Email"));
builder.Services.AddSingleton<IEmailSenderFactory, EmailSenderFactory>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<IOrderService>(provider =>
{
    var service = provider.GetRequiredService<OrderService>();
    var withValidation = new ValidationDecorator(service);
    var withLogging = new LoggingDecorator(withValidation);
    return withLogging;
});
```

## Next Steps

1. Complete all 7 parts
2. Do the challenge exercise
3. Review your code from past projects
4. Identify where these patterns would help
5. Move to Module 2: Asynchronous Processing

---

**Ready to level up?** Open [EXERCISE.md](EXERCISE.md) and start with Part 1! 🎯
