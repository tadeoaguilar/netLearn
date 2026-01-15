# Exercise: Understanding Service Lifetimes in .NET DI

## Overview
Service lifetimes are one of the most critical concepts in Dependency Injection. Choosing the wrong lifetime can lead to memory leaks, threading issues, or unexpected behavior. This exercise will help you master Transient, Scoped, and Singleton lifetimes.

## Learning Goals
- Understand what Transient, Scoped, and Singleton mean
- See when instances are created and disposed
- Learn when to use each lifetime
- Identify and avoid the "captive dependency" anti-pattern
- Understand memory and performance implications

---

## Part 1: Understanding Transient Lifetime

### What is Transient?
**Transient services** are created **every time** they are requested from the container.

**Use case**: Lightweight, stateless services

### Step 1.1: Create a Transient Service

**Your Task:**
Create a file called `Services/TransientService.cs`:

```csharp
namespace DILifetimes.Services;

public interface ITransientService
{
    Guid InstanceId { get; }
    void DoWork();
}

public class TransientService : ITransient Service
{
    private readonly Guid _instanceId;

    public TransientService()
    {
        _instanceId = Guid.NewGuid();
        Console.WriteLine($"[TRANSIENT] Created instance {_instanceId}");
    }

    public Guid InstanceId => _instanceId;

    public void DoWork()
    {
        Console.WriteLine($"[TRANSIENT] Working with instance {_instanceId}");
    }
}
```

### Step 1.2: Register as Transient

Update your `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DILifetimes.Services;

Console.WriteLine("=== TRANSIENT LIFETIME ===\n");

var builder = Host.CreateApplicationBuilder(args);

// Register as Transient - NEW instance every time
builder.Services.AddTransient<IOperationService, OperationService>();

var host = builder.Build();

// Request the service 3 times
Console.WriteLine("Requesting service 3 times:");
var service1 = host.Services.GetRequiredService<IOperationService>();
var service2 = host.Services.GetRequiredService<IOperationService>();
var service3 = host.Services.GetRequiredService<IOperationService>();

Console.WriteLine($"\nService 1 ID: {service1.GetOperationId()}");
Console.WriteLine($"Service 2 ID: {service2.GetOperationId()}");
Console.WriteLine($"Service 3 ID: {service3.GetOperationId()}");

Console.WriteLine($"\nAre they the same instance? {service1.GetOperationId() == service2.GetOperationId()}");
```

**Run it:**
```bash
dotnet run
```

**Observe:**
- Constructor called 3 times
- Each service has a DIFFERENT GUID
- Every resolution creates a NEW instance

**When to use Transient:**
- Lightweight, stateless services
- Services that don't hold state between calls
- Examples: Validators, formatters, calculators

---

## Part 2: Singleton Lifetime

### Step 2.1: Understanding Singleton

**Singleton** = ONE instance for the entire application lifetime.

**Your Task:**
Update `Program.cs` to use Singleton:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DILifetimes.Services;

Console.WriteLine("=== SINGLETON LIFETIME ===\n");

var builder = Host.CreateApplicationBuilder(args);

// Register as Singleton - SINGLE instance for app lifetime
builder.Services.AddSingleton<IOperationService, OperationService>();

var host = builder.Build();

// Request the service 3 times
Console.WriteLine("Requesting service 3 times:");
var service1 = host.Services.GetRequiredService<IOperationService>();
var service2 = host.Services.GetRequiredService<IOperationService>();
var service3 = host.Services.GetRequiredService<IOperationService>();

Console.WriteLine($"\nService 1 ID: {service1.GetOperationId()}");
Console.WriteLine($"Service 2 ID: {service2.GetOperationId()}");
Console.WriteLine($"Service 3 ID: {service3.GetOperationId()}");

Console.WriteLine($"\nAre they the same instance? {service1.GetOperationId() == service2.GetOperationId()}");
```

**Run it:**
```bash
dotnet run
```

**Observe:**
- Constructor called ONCE
- All three services have the SAME GUID
- Same instance reused

**When to use Singleton:**
- Heavy objects (caching the creation)
- Stateless services shared across the app
- Configuration services
- Logging services
- Examples: Caches, configuration readers, connection factories

**Warning:**
- Must be thread-safe!
- Be careful with stateful singletons
- Watch memory usage (lives for entire app)

---

## Part 3: Scoped Lifetime

### Step 3.1: Understanding Scoped

**Scoped** = ONE instance per scope. In web apps, one scope = one HTTP request.

**Your Task:**
Create `Services/ScopedDemo.cs`:

```csharp
namespace DILifetimes.Services;

public class ScopedDemo
{
    private readonly IOperationService _service1;
    private readonly IOperationService _service2;

    public ScopedDemo(IOperationService service1, IOperationService service2)
    {
        _service1 = service1;
        _service2 = service2;
    }

    public void ShowIds()
    {
        Console.WriteLine($"  ScopedDemo - Service 1 ID: {_service1.GetOperationId()}");
        Console.WriteLine($"  ScopedDemo - Service 2 ID: {_service2.GetOperationId()}");
        Console.WriteLine($"  Are they the same? {_service1.GetOperationId() == _service2.GetOperationId()}");
    }
}
```

Update `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DILifetimes.Services;

Console.WriteLine("=== SCOPED LIFETIME ===\n");

var builder = Host.CreateApplicationBuilder(args);

// Register as Scoped - ONE instance per scope
builder.Services.AddScoped<IOperationService, OperationService>();
builder.Services.AddTransient<ScopedDemo>();

var host = builder.Build();

// Create Scope 1
Console.WriteLine("--- Scope 1 ---");
using (var scope1 = host.Services.CreateScope())
{
    var demo1 = scope1.ServiceProvider.GetRequiredService<ScopedDemo>();
    demo1.ShowIds();

    // Request again within same scope
    var demo2 = scope1.ServiceProvider.GetRequiredService<ScopedDemo>();
    demo2.ShowIds();
}

Console.WriteLine();

// Create Scope 2
Console.WriteLine("--- Scope 2 ---");
using (var scope2 = host.Services.CreateScope())
{
    var demo3 = scope2.ServiceProvider.GetRequiredService<ScopedDemo>();
    demo3.ShowIds();
}
```

**Run it:**
```bash
dotnet run
```

**Observe:**
- Within Scope 1: Same GUID for all injections
- Within Scope 2: Different GUID (new scope = new instance)
- New scope = new instance created

**When to use Scoped:**
- Database contexts (EF Core DbContext)
- HTTP request-specific data
- Unit of work pattern
- Per-request services in web apps

---

## Part 4: Side-by-Side Comparison

### Step 4.1: Compare All Three

**Your Task:**
Create `Services/LifetimeDemo.cs`:

```csharp
namespace DILifetimes.Services;

public interface ITransientService
{
    Guid GetId();
}

public interface IScopedService
{
    Guid GetId();
}

public interface ISingletonService
{
    Guid GetId();
}

public class TransientService : ITransientService
{
    private readonly Guid _id;

    public TransientService()
    {
        _id = Guid.NewGuid();
        Console.WriteLine($"[Transient] Created with ID: {_id}");
    }

    public Guid GetId() => _id;
}

public class ScopedService : IScopedService
{
    private readonly Guid _id;

    public ScopedService()
    {
        _id = Guid.NewGuid();
        Console.WriteLine($"[Scoped] Created with ID: {_id}");
    }

    public Guid GetId() => _id;
}

public class SingletonService : ISingletonService
{
    private readonly Guid _id;

    public SingletonService()
    {
        _id = Guid.NewGuid();
        Console.WriteLine($"[Singleton] Created with ID: {_id}");
    }

    public Guid GetId() => _id;
}

public class ServiceConsumer
{
    public ServiceConsumer(
        ITransientService transient,
        IScopedService scoped,
        ISingletonService singleton)
    {
        Console.WriteLine($"  Consumer - Transient: {transient.GetId()}");
        Console.WriteLine($"  Consumer - Scoped: {scoped.GetId()}");
        Console.WriteLine($"  Consumer - Singleton: {singleton.GetId()}");
        Console.WriteLine();
    }
}
```

Update `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DILifetimes.Services;

Console.WriteLine("=== LIFETIME COMPARISON ===\n");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTransient<ITransientService, TransientService>();
builder.Services.AddScoped<IScopedService, ScopedService>();
builder.Services.AddSingleton<ISingletonService, SingletonService>();
builder.Services.AddTransient<ServiceConsumer>();

var host = builder.Build();

Console.WriteLine("Creating Scope 1:");
using (var scope1 = host.Services.CreateScope())
{
    Console.WriteLine("  Getting ServiceConsumer (1st time):");
    var consumer1 = scope1.ServiceProvider.GetRequiredService<ServiceConsumer>();

    Console.WriteLine("  Getting ServiceConsumer (2nd time):");
    var consumer2 = scope1.ServiceProvider.GetRequiredService<ServiceConsumer>();
}

Console.WriteLine("\nCreating Scope 2:");
using (var scope2 = host.Services.CreateScope())
{
    Console.WriteLine("  Getting ServiceConsumer:");
    var consumer3 = scope2.ServiceProvider.GetRequiredService<ServiceConsumer>();
}
```

**Run it and observe:**
- **Transient**: New instance every time (6 different GUIDs)
- **Scoped**: Same within scope, different across scopes (2 GUIDs total)
- **Singleton**: Same always (1 GUID total)

---

## Part 5: Captive Dependencies (Common Pitfall!)

### Step 5.1: The Captive Dependency Problem

**DANGER ZONE**: What happens when a Singleton captures a Scoped service?

**Your Task:**
Create `Services/CaptiveDependency.cs`:

```csharp
namespace DILifetimes.Services;

public interface IDatabaseContext
{
    Guid GetConnectionId();
}

public class DatabaseContext : IDatabaseContext
{
    private readonly Guid _connectionId;

    public DatabaseContext()
    {
        _connectionId = Guid.NewGuid();
        Console.WriteLine($"[DbContext] New connection created: {_connectionId}");
    }

    public Guid GetConnectionId() => _connectionId;
}

// WRONG: Singleton depending on Scoped
public class CaptiveDependencyExample
{
    private readonly IDatabaseContext _dbContext;

    public CaptiveDependencyExample(IDatabaseContext dbContext)
    {
        _dbContext = dbContext;
        Console.WriteLine("[Singleton] Captured DbContext in constructor");
    }

    public void DoWork()
    {
        Console.WriteLine($"Using DbContext: {_dbContext.GetConnectionId()}");
    }
}
```

Update `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DILifetimes.Services;

Console.WriteLine("=== CAPTIVE DEPENDENCY PROBLEM ===\n");

var builder = Host.CreateApplicationBuilder(args);

// This is WRONG but let's see what happens
builder.Services.AddScoped<IDatabaseContext, DatabaseContext>();
builder.Services.AddSingleton<CaptiveDependencyExample>();

var host = builder.Build();

Console.WriteLine("Scope 1:");
using (var scope1 = host.Services.CreateScope())
{
    var example = scope1.ServiceProvider.GetRequiredService<CaptiveDependencyExample>();
    example.DoWork();
}

Console.WriteLine("\nScope 2:");
using (var scope2 = host.Services.CreateScope())
{
    var example = scope2.ServiceProvider.GetRequiredService<CaptiveDependencyExample>();
    example.DoWork();
}

Console.WriteLine("\nPROBLEM: DbContext created only once!");
Console.WriteLine("The Singleton 'captured' the first Scoped instance.");
Console.WriteLine("Both scopes use the SAME DbContext - this is BAD!");
```

**Run it and observe the problem:**
- DbContext created only ONCE
- Same connection used across different scopes
- In a web app, this means sharing DbContext across HTTP requests!
- Can cause data corruption, concurrency issues

**The Rule:**
- Singleton can depend on: Singleton (OK)
- Scoped can depend on: Singleton, Scoped (OK)
- Transient can depend on: Singleton, Scoped, Transient (OK)
- **NEVER**: Long-lived service depending on short-lived service

### Step 5.2: The Correct Solution

**Your Task:**
Create `Services/CorrectPattern.cs`:

```csharp
namespace DILifetimes.Services;

// CORRECT: Singleton depends on IServiceProvider
public class CorrectSingletonExample
{
    private readonly IServiceProvider _serviceProvider;

    public CorrectSingletonExample(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Console.WriteLine("[Singleton] Storing IServiceProvider (not the scoped service)");
    }

    public void DoWork()
    {
        // Create a scope when needed
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDatabaseContext>();
        Console.WriteLine($"Using DbContext: {dbContext.GetConnectionId()}");
    }
}
```

Update `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DILifetimes.Services;

Console.WriteLine("=== CORRECT PATTERN ===\n");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<IDatabaseContext, DatabaseContext>();
builder.Services.AddSingleton<CorrectSingletonExample>();

var host = builder.Build();

var example = host.Services.GetRequiredService<CorrectSingletonExample>();

Console.WriteLine("Call 1:");
example.DoWork();

Console.WriteLine("\nCall 2:");
example.DoWork();

Console.WriteLine("\nCORRECT: New DbContext created each time!");
```

**Observe:**
- Each call creates a NEW DbContext
- Singleton doesn't capture the Scoped service
- Proper lifetime management

---

## Part 6: Practical Scenarios

### Step 6.1: Real-World Examples

**Your Task:**
Create `Services/RealWorldExamples.cs`:

```csharp
namespace DILifetimes.Services;

// Transient: Stateless validator
public class EmailValidator
{
    public EmailValidator()
    {
        Console.WriteLine("[Transient] EmailValidator created");
    }

    public bool IsValid(string email)
    {
        return !string.IsNullOrWhiteSpace(email) && email.Contains("@");
    }
}

// Singleton: Configuration reader
public class AppConfiguration
{
    private readonly Dictionary<string, string> _settings;

    public AppConfiguration()
    {
        Console.WriteLine("[Singleton] AppConfiguration created");
        _settings = new Dictionary<string, string>
        {
            ["AppName"] = "DILifetimes Demo",
            ["Version"] = "1.0.0"
        };
    }

    public string GetSetting(string key) => _settings.GetValueOrDefault(key, "Not Found");
}

// Scoped: Database context (simulated)
public class OrderRepository
{
    private readonly Guid _transactionId;

    public OrderRepository()
    {
        _transactionId = Guid.NewGuid();
        Console.WriteLine($"[Scoped] OrderRepository created with transaction: {_transactionId}");
    }

    public void SaveOrder(string order)
    {
        Console.WriteLine($"[Scoped] Saving order '{order}' in transaction: {_transactionId}");
    }

    public Guid GetTransactionId() => _transactionId;
}

// Service using all three
public class OrderService
{
    private readonly EmailValidator _validator;
    private readonly AppConfiguration _config;
    private readonly OrderRepository _repository;

    public OrderService(
        EmailValidator validator,
        AppConfiguration config,
        OrderRepository repository)
    {
        _validator = validator;
        _config = config;
        _repository = repository;
    }

    public void ProcessOrder(string customerEmail, string orderDetails)
    {
        Console.WriteLine($"\n[{_config.GetSetting("AppName")}] Processing order...");

        if (!_validator.IsValid(customerEmail))
        {
            Console.WriteLine("  Invalid email!");
            return;
        }

        _repository.SaveOrder(orderDetails);
        Console.WriteLine($"  Order processed in transaction: {_repository.GetTransactionId()}");
    }
}
```

Update `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DILifetimes.Services;

Console.WriteLine("=== REAL-WORLD SCENARIO ===\n");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTransient<EmailValidator>();
builder.Services.AddSingleton<AppConfiguration>();
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddTransient<OrderService>();

var host = builder.Build();

// Simulate two HTTP requests (two scopes)
Console.WriteLine("Request 1 (Scope 1):");
using (var scope1 = host.Services.CreateScope())
{
    var orderService = scope1.ServiceProvider.GetRequiredService<OrderService>();
    orderService.ProcessOrder("customer1@example.com", "Order #1");
}

Console.WriteLine("\n" + new string('-', 60));
Console.WriteLine("Request 2 (Scope 2):");
using (var scope2 = host.Services.CreateScope())
{
    var orderService = scope2.ServiceProvider.GetRequiredService<OrderService>();
    orderService.ProcessOrder("customer2@example.com", "Order #2");
}

Console.WriteLine("\nObservations:");
Console.WriteLine("- EmailValidator: Created multiple times (Transient)");
Console.WriteLine("- AppConfiguration: Created once (Singleton)");
Console.WriteLine("- OrderRepository: New per scope/request (Scoped)");
```

---

## Part 7: Challenge Exercise

### Challenge: Build a Request Pipeline

**Your Task:**
Create a request processing pipeline with proper lifetimes:

1. **RequestIdGenerator** (Scoped)
   - Generates unique ID per request
   - Should be same for entire request

2. **Logger** (Singleton)
   - Logs all operations
   - Shared across all requests

3. **RequestValidator** (Transient)
   - Validates requests
   - Stateless, new instance OK

4. **RequestProcessor** (Transient)
   - Processes requests
   - Uses all three above

5. Create two scopes (simulating two requests)
6. Show that RequestIdGenerator is different per scope
7. Show that Logger is the same singleton

**Bonus:**
Add a **CacheService** (Singleton) that stores processed requests and their IDs.

---

## Reflection Questions

1. **When would you use each lifetime?**
   - Transient: _____________
   - Scoped: _____________
   - Singleton: _____________

2. **What is a captive dependency?**
   - Definition: _____________
   - Why is it bad: _____________

3. **In a web API, what lifetime should a DbContext have?**
   - Answer: _____________
   - Why: _____________

4. **Can a Scoped service depend on a Singleton? Why/why not?**

5. **What are the memory implications of using Singleton for large objects?**

---

## Summary

You've learned:
- ✅ **Transient**: New instance every time
- ✅ **Scoped**: One instance per scope (per request in web apps)
- ✅ **Singleton**: One instance for application lifetime
- ✅ **Captive Dependency**: Anti-pattern to avoid
- ✅ **Lifetime Rules**: Long-lived cannot depend on short-lived
- ✅ **Real-world scenarios**: When to use each

## Quick Reference

| Lifetime | Created | Disposed | Use Case |
|----------|---------|----------|----------|
| **Transient** | Every resolution | Immediately | Lightweight, stateless services |
| **Scoped** | Once per scope | End of scope | DbContext, per-request services |
| **Singleton** | Once per app | App shutdown | Configuration, caches, shared state |

## Next Steps

Move on to **[AdvancedDI](../AdvancedDI/)** to learn:
- Factory patterns
- Decorator pattern
- Named services
- Open generic types

---

**Happy Learning! 🚀**
