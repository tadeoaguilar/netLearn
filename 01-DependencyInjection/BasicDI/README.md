# BasicDI - Dependency Injection Fundamentals

## Overview
This project teaches you the fundamentals of Dependency Injection (DI) in .NET through hands-on exercises. You'll build a user registration system and learn how to refactor tightly-coupled code into a flexible, testable architecture.

## What You'll Learn

### Core Concepts
- **Tight Coupling vs Loose Coupling**: Understand the problems with tightly-coupled code
- **Dependency Inversion Principle**: Depend on abstractions, not concretions
- **Constructor Injection**: The primary pattern for injecting dependencies
- **Interface-Based Design**: Using interfaces to define contracts
- **DI Container**: Using Microsoft's built-in DI container to manage dependencies

### Practical Skills
- Identifying tightly-coupled code
- Refactoring to use dependency injection
- Configuring service registration
- Swapping implementations without code changes
- Understanding service lifetimes (Transient, Singleton)

## Project Structure

```
BasicDI/
├── BasicDI/                    # Main project folder
│   ├── BasicDI.csproj         # Project file
│   ├── Program.cs             # Entry point (you'll modify)
│   ├── TightlyCoupled/        # Part 1: Anti-pattern examples
│   │   └── UserService.cs
│   └── WithDI/                # Parts 2-6: Proper DI
│       ├── Interfaces.cs
│       ├── Implementations.cs
│       ├── AlternativeImplementations.cs
│       ├── UserService.cs
│       └── ServiceWithState.cs
├── EXERCISE.md                # Step-by-step exercise guide
├── GETTING_STARTED.md         # Quick start instructions
└── README.md                  # This file
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK (already installed)
- Your favorite code editor (VS Code, Visual Studio, Rider)
- Basic C# knowledge

### Quick Start

1. **Navigate to the project:**
   ```bash
   cd BasicDI/BasicDI
   ```

2. **Verify setup:**
   ```bash
   dotnet build
   ```

3. **Read the getting started guide:**
   Open [GETTING_STARTED.md](GETTING_STARTED.md)

4. **Start the exercise:**
   Open [EXERCISE.md](EXERCISE.md) and follow Part 1

### The Learning Path

**Follow this sequence:**

1. **Part 1: Tight Coupling** (15 minutes)
   - See what's wrong with tightly-coupled code
   - Run the anti-pattern example
   - Understand the limitations

2. **Part 2: Introducing DI** (30 minutes)
   - Create interfaces
   - Implement constructor injection
   - Manually inject dependencies

3. **Part 3: DI Container** (20 minutes)
   - Configure Microsoft's DI container
   - Register services
   - Resolve dependencies automatically

4. **Part 4: Swapping Implementations** (25 minutes)
   - Create alternative implementations
   - Change behavior without touching code
   - See the power of abstraction

5. **Part 5: Service Lifetimes** (20 minutes)
   - Understand Transient vs Singleton
   - Observe instance creation
   - Choose appropriate lifetimes

6. **Part 6: Challenge** (30+ minutes)
   - Apply your learning
   - Build a validation service
   - Practice the full DI workflow

**Total Time**: ~2-3 hours

## Key Takeaways

After completing this project, you should be able to:

✅ Identify tightly-coupled code and explain why it's problematic
✅ Refactor code to use dependency injection
✅ Create and use interfaces for abstraction
✅ Inject dependencies through constructors
✅ Configure and use the .NET DI container
✅ Register services with appropriate lifetimes
✅ Swap implementations without modifying dependent code
✅ Write more testable and maintainable code

## Examples Covered

### Before DI (Tight Coupling)
```csharp
public class UserService
{
    private readonly EmailSender _emailSender = new EmailSender();

    public void RegisterUser(string username, string email)
    {
        _emailSender.SendEmail(email, "Welcome", "...");
    }
}
```
**Problems**: Can't test without sending real emails, can't swap email providers

### After DI (Loose Coupling)
```csharp
public class UserService
{
    private readonly IEmailSender _emailSender;

    public UserService(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public void RegisterUser(string username, string email)
    {
        _emailSender.SendEmail(email, "Welcome", "...");
    }
}
```
**Benefits**: Easy to test, can swap implementations, explicit dependencies

### DI Container Configuration
```csharp
// Register services
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<UserService>();

// Container resolves all dependencies automatically
var userService = host.Services.GetRequiredService<UserService>();
```

## Testing Your Understanding

After completing the exercises, try to answer:

1. What is the primary benefit of depending on interfaces vs concrete classes?
2. Why is constructor injection preferred over property injection?
3. When would you use Transient lifetime vs Singleton?
4. How does DI make code more testable?
5. What is the role of the DI container?

## Common Mistakes to Avoid

❌ Creating dependencies with `new` inside classes
❌ Using concrete classes instead of interfaces as dependencies
❌ Exposing setters for dependencies (use readonly fields)
❌ Choosing wrong service lifetimes
❌ Over-abstracting simple code

## Next Steps

After completing BasicDI, continue your learning with:

1. **[DILifetimes](../DILifetimes/)** - Deep dive into Transient, Scoped, and Singleton
   - Understand when to use each lifetime
   - Avoid captive dependencies
   - Manage stateful services

2. **[AdvancedDI](../AdvancedDI/)** - Advanced DI patterns
   - Factory patterns
   - Decorators
   - Named services
   - Service provider scopes

## Additional Resources

- [Microsoft DI Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- [Dependency Inversion Principle](https://en.wikipedia.org/wiki/Dependency_inversion_principle)

## Tips for Success

1. **Type the code yourself** - Don't copy-paste. Muscle memory helps learning.
2. **Experiment freely** - Try breaking things to understand how they work.
3. **Run frequently** - Run your code after each change to see immediate results.
4. **Think about real scenarios** - How would you apply this to your projects?
5. **Ask questions** - If something doesn't make sense, investigate or ask for help.

---

**Ready to begin?** Open [GETTING_STARTED.md](GETTING_STARTED.md) to start your journey! 🚀
