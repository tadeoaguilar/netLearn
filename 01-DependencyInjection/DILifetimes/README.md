# DILifetimes - Understanding Service Lifetimes

## Overview
Master the three service lifetimes in .NET Dependency Injection: Transient, Scoped, and Singleton. Understanding when to use each lifetime is critical for building efficient, bug-free applications.

## What You'll Learn

### Core Concepts
- **Transient Lifetime**: New instance for every resolution
- **Scoped Lifetime**: One instance per scope (per HTTP request in web apps)
- **Singleton Lifetime**: Single instance for application lifetime
- **Captive Dependencies**: The most common lifetime anti-pattern
- **Lifetime Rules**: Which lifetimes can depend on which

### Critical Skills
- Choosing the correct lifetime for different scenarios
- Identifying captive dependency bugs before they happen
- Understanding memory and performance implications
- Avoiding common pitfalls in web applications
- Using IServiceProvider correctly for dynamic resolution

## Why Service Lifetimes Matter

### The Wrong Lifetime Can Cause:
- ❌ **Memory leaks**: Holding references too long
- ❌ **Concurrency bugs**: Sharing state across threads
- ❌ **Data corruption**: Sharing DbContext across requests
- ❌ **Performance issues**: Creating expensive objects repeatedly
- ❌ **Unexpected behavior**: Services with wrong state

### The Right Lifetime Ensures:
- ✅ **Correctness**: Right instance at the right time
- ✅ **Performance**: Optimal object creation/disposal
- ✅ **Thread safety**: No unintended sharing
- ✅ **Predictability**: Behavior matches expectations
- ✅ **Maintainability**: Clear lifetime semantics

## Project Structure

```
DILifetimes/
├── DILifetimes/
│   ├── DILifetimes.csproj
│   ├── Program.cs
│   └── Services/
│       ├── LifetimeDemo.cs
│       ├── CaptiveDependency.cs
│       ├── CorrectPattern.cs
│       └── RealWorldExamples.cs
├── EXERCISE.md
├── GETTING_STARTED.md
└── README.md
```

## Quick Start

1. **Navigate to project:**
   ```bash
   cd DILifetimes/DILifetimes
   ```

2. **Verify setup:**
   ```bash
   dotnet build
   ```

3. **Start learning:**
   Open [EXERCISE.md](EXERCISE.md)

## The Learning Path

### Part 1: Transient (20 min)
- See instances created every time
- Understand when to use Transient
- Examples: Validators, calculators

### Part 2: Singleton (15 min)
- See single instance reused
- Understand thread safety requirements
- Examples: Configuration, caches

### Part 3: Scoped (25 min)
- Understand scope lifecycle
- See instances per scope
- Examples: DbContext, UnitOfWork

### Part 4: Side-by-Side (20 min)
- Compare all three lifetimes
- See the differences visually
- Understand when instances are created

### Part 5: Captive Dependencies (30 min)
- **CRITICAL**: Learn the most common mistake
- See what goes wrong
- Learn the correct pattern

### Part 6: Real-World Scenarios (25 min)
- Practical examples
- Combined usage patterns
- Web application simulation

### Part 7: Challenge (30+ min)
- Apply everything learned
- Build request pipeline
- Practice lifetime selection

**Total Time**: ~2.5-3 hours

## Key Takeaways

### Transient
```csharp
builder.Services.AddTransient<IValidator, EmailValidator>();
```
- **When**: Lightweight, stateless services
- **Created**: Every time requested
- **Disposed**: Immediately after scope ends
- **Thread Safe**: N/A (new instance each time)
- **Memory**: Low (short-lived)

### Scoped
```csharp
builder.Services.AddScoped<DbContext>();
```
- **When**: Per-request services (web apps)
- **Created**: Once per scope
- **Disposed**: When scope is disposed
- **Thread Safe**: Within scope only
- **Memory**: Moderate (scope duration)

### Singleton
```csharp
builder.Services.AddSingleton<IConfiguration>();
```
- **When**: Expensive, stateless, shared services
- **Created**: First time requested
- **Disposed**: Application shutdown
- **Thread Safe**: MUST BE thread-safe
- **Memory**: Lives forever (be careful!)

## The Captive Dependency Problem

### ❌ WRONG
```csharp
// Singleton captures Scoped - BUG!
public class MySingleton
{
    private readonly DbContext _db;  // Scoped service captured!

    public MySingleton(DbContext db)
    {
        _db = db;  // This DbContext will live forever!
    }
}
```

### ✅ CORRECT
```csharp
// Singleton uses IServiceProvider
public class MySingleton
{
    private readonly IServiceProvider _serviceProvider;

    public MySingleton(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void DoWork()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        // Use db...
    }
}
```

## Lifetime Dependency Rules

| Service Lifetime | Can Depend On |
|------------------|---------------|
| **Transient** | Transient, Scoped, Singleton |
| **Scoped** | Scoped, Singleton |
| **Singleton** | Singleton only |

**Rule**: Longer-lived services should NOT depend on shorter-lived services.

## Common Scenarios

### Web API Example
```csharp
builder.Services.AddSingleton<IConfiguration>();       // App-wide config
builder.Services.AddScoped<DbContext>();              // One per request
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>(); // One per request
builder.Services.AddTransient<IValidator>();          // New each time
builder.Services.AddTransient<IMapper>();             // New each time
```

### Background Service Example
```csharp
builder.Services.AddSingleton<ILogger>();             // Shared logger
builder.Services.AddSingleton<IBackgroundQueue>();    // Shared queue
builder.Services.AddHostedService<QueueProcessor>();  // Singleton background service

// Inside background service, create scopes for work
public class QueueProcessor : BackgroundService
{
    private readonly IServiceProvider _services;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            // Process item...
        }
    }
}
```

## Real-World Pitfalls

### Pitfall 1: Singleton DbContext
```csharp
// NEVER DO THIS!
builder.Services.AddSingleton<DbContext>();
```
**Problem**: DbContext shared across all requests = data corruption

### Pitfall 2: Captive Dependency in Web API
```csharp
// Controller (Scoped in web apps)
public class OrderController
{
    // Singleton capturing HttpContext is BAD!
    private readonly IHttpContextAccessor _accessor;
}
```

### Pitfall 3: Disposable Transients
```csharp
// Transient service implementing IDisposable
// Container will track it until scope ends
// Can cause memory issues if many created
```

## Performance Considerations

### Transient
- **Pro**: No memory retention
- **Con**: Object creation overhead
- **Tip**: Fine for lightweight objects

### Scoped
- **Pro**: Reused within scope
- **Con**: Held in memory for scope duration
- **Tip**: Perfect for DbContext

### Singleton
- **Pro**: Created once, maximum reuse
- **Con**: Lives forever, must be thread-safe
- **Tip**: Great for expensive, stateless objects

## Testing with Different Lifetimes

```csharp
// In tests, you can override lifetimes
var services = new ServiceCollection();
services.AddScoped<IRepository, FakeRepository>();  // Normally Scoped
// In tests, each test creates its own scope
```

## Common Questions

**Q: Can I change a service's lifetime later?**
A: Yes, but be careful! Last registration wins.

**Q: What lifetime for EF Core DbContext?**
A: Scoped - one per HTTP request.

**Q: What if I'm not sure?**
A: Start with Transient (safest), optimize later if needed.

**Q: How do I know if I have a captive dependency?**
A: .NET 8+ warns you in development mode!

**Q: Can a Scoped service depend on a Transient?**
A: Yes, that's fine. New Transient created each time.

## Next Steps

After mastering lifetimes:

1. **[AdvancedDI](../AdvancedDI/)** - Factory patterns, decorators
2. Apply to real projects
3. Review your existing code for lifetime issues
4. Share knowledge with your team

## Checklist

After completing this module, you should be able to:

- [ ] Explain the difference between Transient, Scoped, and Singleton
- [ ] Choose the appropriate lifetime for a service
- [ ] Identify captive dependency anti-patterns
- [ ] Fix captive dependencies using IServiceProvider
- [ ] Understand when instances are created and disposed
- [ ] Apply lifetimes correctly in web applications
- [ ] Avoid common lifetime pitfalls

---

**Ready to master service lifetimes?** Start with [EXERCISE.md](EXERCISE.md)! 🚀
