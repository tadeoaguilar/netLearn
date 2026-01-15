# Exercise: Understanding Dependency Injection Basics

## Overview
In this exercise, you'll learn Dependency Injection by building a simple notification system. You'll start with tightly-coupled code (the wrong way) and refactor it step-by-step to use proper DI patterns.

## Learning Goals
By completing this exercise, you will:
- Understand the problems with tight coupling
- Learn to depend on abstractions (interfaces)
- Use constructor injection
- Configure and use .NET's built-in DI container
- Understand the benefits of DI for testing and maintenance

---

## The Scenario

You're building a **User Registration System** that needs to:
1. Validate user data
2. Save the user to a database
3. Send a welcome email
4. Log the registration event

---

## Part 1: The Tightly-Coupled Approach (Anti-Pattern)

### Step 1.1: Create the Tightly-Coupled Version

First, let's see the **WRONG WAY** - code that is tightly coupled and hard to test.

**Your Task:**
Create a file called `TightlyCoupled/UserService.cs` with the following classes:

```csharp
namespace BasicDI.TightlyCoupled;

// Concrete implementation - directly used
public class EmailSender
{
    public void SendEmail(string to, string subject, string body)
    {
        Console.WriteLine($"[EMAIL] To: {to}");
        Console.WriteLine($"[EMAIL] Subject: {subject}");
        Console.WriteLine($"[EMAIL] Body: {body}");
        Console.WriteLine();
    }
}

// Another concrete implementation
public class DatabaseRepository
{
    public void SaveUser(string username, string email)
    {
        Console.WriteLine($"[DATABASE] Saving user: {username} ({email})");
        Console.WriteLine();
    }
}

// Logger
public class Logger
{
    public void Log(string message)
    {
        Console.WriteLine($"[LOG] {DateTime.Now:HH:mm:ss} - {message}");
        Console.WriteLine();
    }
}

// UserService with TIGHT COUPLING - BAD!
public class UserService
{
    // Concrete dependencies created inside the class
    private readonly EmailSender _emailSender = new EmailSender();
    private readonly DatabaseRepository _repository = new DatabaseRepository();
    private readonly Logger _logger = new Logger();

    public void RegisterUser(string username, string email)
    {
        _logger.Log($"Starting registration for {username}");

        // Validate
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
        {
            _logger.Log("Validation failed");
            throw new ArgumentException("Username and email are required");
        }

        // Save to database
        _repository.SaveUser(username, email);

        // Send welcome email
        _emailSender.SendEmail(email, "Welcome!", $"Hello {username}, welcome to our platform!");

        _logger.Log($"Registration completed for {username}");
    }
}
```

**Questions to think about:**
1. What if you want to test `UserService` without sending real emails?
2. What if you want to use a different email provider (SendGrid, MailChimp)?
3. What if you want to save to a different database?
4. How would you write unit tests for this?

### Step 1.2: Test the Tightly-Coupled Version

Update your `Program.cs` to test this:

```csharp
using BasicDI.TightlyCoupled;

Console.WriteLine("=== TIGHTLY COUPLED VERSION ===\n");

var userService = new UserService();
userService.RegisterUser("john_doe", "john@example.com");

Console.WriteLine("\nPress any key to continue...");
Console.ReadKey();
```

**Run it:**
```bash
dotnet run
```

**Observe:**
- It works, but `UserService` is tightly bound to specific implementations
- You cannot swap implementations
- Testing would require real email sending and database access

---

## Part 2: Introducing Dependency Injection

### Step 2.1: Create Abstractions (Interfaces)

The first step to DI is to **depend on abstractions, not concretions**.

**Your Task:**
Create a file called `WithDI/Interfaces.cs`:

```csharp
namespace BasicDI.WithDI;

public interface IEmailSender
{
    void SendEmail(string to, string subject, string body);
}

public interface IUserRepository
{
    void SaveUser(string username, string email);
}

public interface ILogger
{
    void Log(string message);
}
```

**Key Concept:**
- Interfaces define **what** to do, not **how** to do it
- Code should depend on these interfaces
- Concrete implementations can be swapped

### Step 2.2: Create Implementations

**Your Task:**
Create a file called `WithDI/Implementations.cs`:

```csharp
namespace BasicDI.WithDI;

// Concrete implementation of IEmailSender
public class EmailSender : IEmailSender
{
    public void SendEmail(string to, string subject, string body)
    {
        Console.WriteLine($"[EMAIL] To: {to}");
        Console.WriteLine($"[EMAIL] Subject: {subject}");
        Console.WriteLine($"[EMAIL] Body: {body}");
        Console.WriteLine();
    }
}

// Concrete implementation of IUserRepository
public class UserRepository : IUserRepository
{
    public void SaveUser(string username, string email)
    {
        Console.WriteLine($"[DATABASE] Saving user: {username} ({email})");
        Console.WriteLine();
    }
}

// Concrete implementation of ILogger
public class ConsoleLogger : ILogger
{
    public void Log(string message)
    {
        Console.WriteLine($"[LOG] {DateTime.Now:HH:mm:ss} - {message}");
        Console.WriteLine();
    }
}
```

### Step 2.3: Refactor UserService with Constructor Injection

**Your Task:**
Create a file called `WithDI/UserService.cs`:

```csharp
namespace BasicDI.WithDI;

// UserService now depends on INTERFACES, not concrete classes
public class UserService
{
    private readonly IEmailSender _emailSender;
    private readonly IUserRepository _repository;
    private readonly ILogger _logger;

    // Constructor Injection - dependencies are injected from outside
    public UserService(
        IEmailSender emailSender,
        IUserRepository repository,
        ILogger logger)
    {
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RegisterUser(string username, string email)
    {
        _logger.Log($"Starting registration for {username}");

        // Validate
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
        {
            _logger.Log("Validation failed");
            throw new ArgumentException("Username and email are required");
        }

        // Save to database
        _repository.SaveUser(username, email);

        // Send welcome email
        _emailSender.SendEmail(email, "Welcome!", $"Hello {username}, welcome to our platform!");

        _logger.Log($"Registration completed for {username}");
    }
}
```

**Key Changes:**
1. Dependencies are now **interfaces**
2. Dependencies are **injected** through the constructor
3. No `new` keyword for dependencies inside the class
4. Null checks for safety

**Benefits:**
- Easy to test (inject mock implementations)
- Easy to swap implementations
- Dependencies are explicit and visible
- Follows Dependency Inversion Principle

### Step 2.4: Manual Dependency Injection

Update your `Program.cs` to manually inject dependencies:

```csharp
using BasicDI.WithDI;

Console.WriteLine("\n=== WITH DEPENDENCY INJECTION (Manual) ===\n");

// Manually create dependencies
IEmailSender emailSender = new EmailSender();
IUserRepository repository = new UserRepository();
ILogger logger = new ConsoleLogger();

// Inject dependencies into UserService
var userService = new UserService(emailSender, repository, logger);
userService.RegisterUser("jane_doe", "jane@example.com");

Console.WriteLine("\nPress any key to continue...");
Console.ReadKey();
```

**Run it:**
```bash
dotnet run
```

**Observation:**
- Same functionality, but now we control the dependencies
- We can easily swap implementations

---

## Part 3: Using a DI Container

Manual injection works but becomes tedious in large applications. That's where DI containers help!

### Step 3.1: Configure the DI Container

**Your Task:**
Update your `Program.cs` to use Microsoft's DI container:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BasicDI.WithDI;

Console.WriteLine("\n=== WITH DEPENDENCY INJECTION (DI Container) ===\n");

// Create and configure the DI container
var builder = Host.CreateApplicationBuilder(args);

// Register services (tell the container what to inject)
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<IUserRepository, UserRepository>();
builder.Services.AddTransient<ILogger, ConsoleLogger>();
builder.Services.AddTransient<UserService>();

// Build the service provider
var host = builder.Build();

// Resolve UserService (container automatically injects dependencies)
var userService = host.Services.GetRequiredService<UserService>();
userService.RegisterUser("alice_wonder", "alice@example.com");

Console.WriteLine("\nDI Container handled all the wiring automatically!");
```

**Key Concepts:**
- `AddTransient<Interface, Implementation>()` - Register a service
- Container automatically resolves dependencies
- No manual `new` calls needed

### Step 3.2: Run and Observe

```bash
dotnet run
```

**What Happened:**
1. You registered services with the container
2. When you asked for `UserService`, the container:
   - Saw it needs `IEmailSender`, `IUserRepository`, `ILogger`
   - Created instances of `EmailSender`, `UserRepository`, `ConsoleLogger`
   - Created `UserService` with those dependencies
   - Gave you a fully configured `UserService`

---

## Part 4: Create Alternative Implementations

Now let's see the power of DI - swapping implementations!

### Step 4.1: Create Alternative Implementations

**Your Task:**
Create a file called `WithDI/AlternativeImplementations.cs`:

```csharp
namespace BasicDI.WithDI;

// Alternative email sender (e.g., for testing or different provider)
public class FakeEmailSender : IEmailSender
{
    public void SendEmail(string to, string subject, string body)
    {
        Console.WriteLine($"[FAKE EMAIL] Would send to {to}: {subject}");
        Console.WriteLine();
    }
}

// Alternative repository (e.g., for testing or different database)
public class InMemoryUserRepository : IUserRepository
{
    private readonly List<(string username, string email)> _users = new();

    public void SaveUser(string username, string email)
    {
        _users.Add((username, email));
        Console.WriteLine($"[IN-MEMORY] Saved user {username}. Total users: {_users.Count}");
        Console.WriteLine();
    }
}

// Alternative logger (e.g., file logger)
public class FileLogger : ILogger
{
    private readonly string _filePath = "app.log";

    public void Log(string message)
    {
        var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
        Console.WriteLine($"[FILE LOG] Writing to {_filePath}: {message}");
        File.AppendAllText(_filePath, logEntry + Environment.NewLine);
        Console.WriteLine();
    }
}
```

### Step 4.2: Swap Implementations

Update `Program.cs` to use alternative implementations:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BasicDI.WithDI;

Console.WriteLine("\n=== WITH ALTERNATIVE IMPLEMENTATIONS ===\n");

var builder = Host.CreateApplicationBuilder(args);

// Register DIFFERENT implementations - no changes to UserService needed!
builder.Services.AddTransient<IEmailSender, FakeEmailSender>();
builder.Services.AddTransient<IUserRepository, InMemoryUserRepository>();
builder.Services.AddTransient<ILogger, FileLogger>();
builder.Services.AddTransient<UserService>();

var host = builder.Build();

var userService = host.Services.GetRequiredService<UserService>();
userService.RegisterUser("bob_builder", "bob@example.com");
userService.RegisterUser("charlie_brown", "charlie@example.com");

Console.WriteLine("\nSame UserService, different implementations!");
```

**Run it:**
```bash
dotnet run
```

**Observe:**
- `UserService` code didn't change at all
- Just changed the registration in the DI container
- Different behavior with zero code changes to UserService

---

## Part 5: Understanding Service Lifetimes

**Your Task:**
Create a file called `WithDI/ServiceWithState.cs`:

```csharp
namespace BasicDI.WithDI;

public interface IRequestIdGenerator
{
    Guid GetRequestId();
}

public class RequestIdGenerator : IRequestIdGenerator
{
    private readonly Guid _id;

    public RequestIdGenerator()
    {
        _id = Guid.NewGuid();
        Console.WriteLine($"[RequestIdGenerator] Created new instance with ID: {_id}");
    }

    public Guid GetRequestId() => _id;
}
```

Update `Program.cs` to test different lifetimes:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BasicDI.WithDI;

Console.WriteLine("\n=== TESTING SERVICE LIFETIMES ===\n");

var builder = Host.CreateApplicationBuilder(args);

// Try different lifetimes: Transient, Scoped, Singleton
// Uncomment one at a time to see the difference:

// TRANSIENT: New instance every time
builder.Services.AddTransient<IRequestIdGenerator, RequestIdGenerator>();

// SINGLETON: Single instance for application lifetime
// builder.Services.AddSingleton<IRequestIdGenerator, RequestIdGenerator>();

var host = builder.Build();

Console.WriteLine("Resolving service 3 times:");
var gen1 = host.Services.GetRequiredService<IRequestIdGenerator>();
var gen2 = host.Services.GetRequiredService<IRequestIdGenerator>();
var gen3 = host.Services.GetRequiredService<IRequestIdGenerator>();

Console.WriteLine($"\ngen1 ID: {gen1.GetRequestId()}");
Console.WriteLine($"gen2 ID: {gen2.GetRequestId()}");
Console.WriteLine($"gen3 ID: {gen3.GetRequestId()}");

Console.WriteLine($"\nAre they the same? {gen1.GetRequestId() == gen2.GetRequestId()}");
```

**Your Task:**
1. Run with `AddTransient` - observe new instance each time
2. Change to `AddSingleton` - observe same instance every time
3. Note the difference in behavior

---

## Part 6: Challenge Exercise

### Challenge: Add a Validation Service

**Your Task:**
Extend the system with a `IValidator` service:

1. Create `IValidator` interface with a method: `bool ValidateEmail(string email)`
2. Create `EmailValidator` implementation that checks:
   - Email is not null or empty
   - Email contains "@" symbol
   - Email has text before and after "@"
3. Inject `IValidator` into `UserService`
4. Use it in the `RegisterUser` method
5. Register it in the DI container
6. Test it!

**Bonus Challenge:**
Create a second implementation `StrictEmailValidator` that also checks:
- Email ends with ".com", ".org", or ".net"
- Username part is at least 3 characters

Then swap implementations by changing only the DI registration!

---

## Reflection Questions

After completing this exercise, answer these:

1. **What are the main benefits of Dependency Injection?**
   - (Think: testing, flexibility, maintenance)

2. **What is the difference between depending on interfaces vs concrete classes?**

3. **When would you use Transient vs Singleton lifetime?**
   - Transient: _______________
   - Singleton: _______________

4. **How does DI help with unit testing?**

5. **What is "Constructor Injection" and why is it preferred?**

---

## Summary

You've learned:
- ✅ Problems with tight coupling
- ✅ Depending on abstractions (interfaces)
- ✅ Constructor injection pattern
- ✅ Using .NET's DI container
- ✅ Service registration and resolution
- ✅ Swapping implementations
- ✅ Service lifetimes (Transient vs Singleton)

## Next Steps

When you're ready, move on to:
- **DILifetimes** - Deep dive into Transient, Scoped, and Singleton
- **AdvancedDI** - Factory patterns, decorators, and advanced scenarios

---

**Happy Learning! 🚀**
