# Getting Started with DILifetimes

## Quick Start

### 1. Navigate to the Project
```bash
cd /Users/tadeo/gitlab/netLearn/01-DependencyInjection/DILifetimes/DILifetimes
```

### 2. Verify Setup
```bash
dotnet build
```

You should see a successful build.

### 3. Project Structure

You'll create this structure as you work through the exercises:

```
DILifetimes/
├── DILifetimes.csproj       # Project file (ready)
├── Program.cs               # You'll edit this repeatedly
├── EXERCISE.md             # Your main guide
├── GETTING_STARTED.md      # This file
└── Services/               # Create this folder
    ├── TransientService.cs
    ├── LifetimeDemo.cs
    ├── CaptiveDependency.cs
    ├── CorrectPattern.cs
    └── RealWorldExamples.cs
```

### 4. Create the Services Folder
```bash
mkdir Services
```

### 5. Start the Exercise

Open [EXERCISE.md](EXERCISE.md) and begin with **Part 1: Transient Lifetime**.

## What Makes This Different from BasicDI

In BasicDI, you learned:
- What DI is
- How to use interfaces
- How to register services

In DILifetimes, you'll learn:
- **WHEN** instances are created
- **HOW LONG** they live
- **WHICH** lifetime to choose
- **WHY** the wrong choice causes bugs

## The Three Lifetimes

### Quick Reference

| Lifetime | Registration | When Created | When Disposed |
|----------|-------------|--------------|---------------|
| Transient | `AddTransient<>()` | Every request | End of scope |
| Scoped | `AddScoped<>()` | Once per scope | End of scope |
| Singleton | `AddSingleton<>()` | First request | App shutdown |

## Running Each Exercise

Each part of the exercise will have you modify `Program.cs`. Here's the typical workflow:

1. **Read the part** in EXERCISE.md
2. **Create the file** mentioned (e.g., `Services/TransientService.cs`)
3. **Update Program.cs** with the provided code
4. **Run it:**
   ```bash
   dotnet run
   ```
5. **Observe the output** carefully
6. **Understand** what happened and why

## Tips for Success

### 1. Focus on the Console Output
The exercises use `Console.WriteLine` to show you when:
- Constructors are called (instance created)
- Services are resolved
- GUIDs are generated (to track instances)

### 2. Pay Attention to GUIDs
Same GUID = Same instance
Different GUID = Different instance

### 3. Understand Scopes
```csharp
using (var scope = host.Services.CreateScope())
{
    // Everything here is in the same scope
    var service1 = scope.ServiceProvider.GetRequiredService<IService>();
    var service2 = scope.ServiceProvider.GetRequiredService<IService>();
    // If IService is Scoped, service1 and service2 are the SAME instance
}
```

### 4. Don't Skip Part 5!
Part 5 (Captive Dependencies) is **THE MOST IMPORTANT** part!
This is the #1 mistake developers make with lifetimes.

### 5. Type the Code
Don't copy-paste. Typing helps you understand and remember.

## Common Commands

```bash
# Build
dotnet build

# Run
dotnet run

# Clean (if you need to rebuild)
dotnet clean
```

## The Critical Concept: Scopes

### In Console Apps
```csharp
using var scope = host.Services.CreateScope();
var service = scope.ServiceProvider.GetRequiredService<IService>();
```

### In Web APIs
ASP.NET Core automatically creates a scope for each HTTP request:
- Request starts → New scope created
- Request processed → Scoped services reused within request
- Request ends → Scope disposed → Scoped services disposed

### Visual Example
```
HTTP Request 1:  [---Scope 1---]
  DbContext A created
  Used multiple times
  Disposed when request ends

HTTP Request 2:  [---Scope 2---]
  DbContext B created (new instance!)
  Used multiple times
  Disposed when request ends
```

## What to Watch For

### ✅ Expected Behavior
```csharp
// Transient: New each time
AddTransient<IService, Service>();
var s1 = provider.GetService<IService>(); // Constructor called
var s2 = provider.GetService<IService>(); // Constructor called again
// s1 != s2 ✅

// Singleton: Same every time
AddSingleton<IService, Service>();
var s1 = provider.GetService<IService>(); // Constructor called
var s2 = provider.GetService<IService>(); // Constructor NOT called
// s1 == s2 ✅
```

### ❌ Captive Dependency (Bug!)
```csharp
AddScoped<IShortLived, ShortLived>();
AddSingleton<ILongLived, LongLived>(); // Depends on IShortLived

// Problem: Singleton captures first Scoped instance
// That Scoped instance never gets disposed!
// In web apps: Sharing data across requests = corruption! ❌
```

## Exercise Progression

### Part 1: Transient (Easy)
Get comfortable with basic concepts. See new instances created.

### Part 2: Singleton (Easy)
See the same instance reused. Understand when to use it.

### Part 3: Scoped (Medium)
Understand the scope lifecycle. Most important for web apps.

### Part 4: Comparison (Medium)
See all three side-by-side. Solidify understanding.

### Part 5: Captive Dependencies (Hard & Critical!)
Learn the most common mistake. Understand why it's wrong.

### Part 6: Real-World (Medium)
Apply to realistic scenarios. See patterns in action.

### Part 7: Challenge (Practice)
Test your understanding. Build something yourself.

## Troubleshooting

### "Namespace not found"
Make sure:
- You created the `Services` folder
- Your namespace is `DILifetimes.Services` in service files
- You have `using DILifetimes.Services;` in Program.cs

### "Cannot resolve service"
Make sure you registered the service:
```csharp
builder.Services.AddTransient<IMyService, MyService>();
```

### "Disposed object accessed"
You tried to use a scoped service after its scope was disposed:
```csharp
IMyService service;
using (var scope = host.Services.CreateScope())
{
    service = scope.ServiceProvider.GetRequiredService<IMyService>();
} // Scope disposed here!
service.DoWork(); // ❌ Object is disposed!
```

## Key Questions to Ask Yourself

As you work through each part:

1. **When was the constructor called?**
   - Once? Multiple times?

2. **Are the GUIDs the same or different?**
   - Same = same instance
   - Different = different instance

3. **When was the object disposed?**
   - End of scope? App shutdown?

4. **Would this work correctly in a web app?**
   - Multiple users hitting the API simultaneously
   - Would they share state incorrectly?

## Real-World Context

### Why This Matters

In a real web application with 1000 concurrent requests:

**With correct lifetimes:**
- ✅ Each request gets its own DbContext (Scoped)
- ✅ All requests share Configuration (Singleton)
- ✅ Validators are created as needed (Transient)
- ✅ No data corruption
- ✅ No memory leaks

**With wrong lifetimes:**
- ❌ Shared DbContext = data corruption
- ❌ Creating Config 1000 times = performance issue
- ❌ Captive dependencies = memory leaks
- ❌ Unpredictable behavior

## After Completing This Module

You'll be able to:
1. Confidently choose service lifetimes
2. Spot lifetime bugs in code reviews
3. Explain lifetimes to teammates
4. Avoid common pitfalls
5. Design services with proper lifetimes

---

## Ready to Start?

1. Make sure you're in the right directory:
   ```bash
   cd /Users/tadeo/gitlab/netLearn/01-DependencyInjection/DILifetimes/DILifetimes
   ```

2. Create the Services folder:
   ```bash
   mkdir Services
   ```

3. Open [EXERCISE.md](EXERCISE.md)

4. Start with Part 1!

**Good luck! This is one of the most important concepts in .NET DI.** 🚀

---

**Need help?** Ask me:
- "I'm getting an error in Part X"
- "Can you explain why [concept] works this way?"
- "I completed Part X, can you review?"
- "I don't understand the difference between Scoped and Transient"
