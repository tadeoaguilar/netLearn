# Module 1: Dependency Injection

## Overview
Dependency Injection (DI) is a fundamental design pattern that enables loose coupling, testability, and maintainability. This module covers DI concepts from basics to advanced scenarios using .NET's built-in IoC container.

## Learning Objectives
- Understand Inversion of Control (IoC) and Dependency Injection
- Master .NET's built-in DI container
- Learn service lifetimes and their implications
- Implement advanced DI patterns

## Projects

### BasicDI
**What you'll learn:**
- What is Dependency Injection and why use it?
- Constructor injection vs property injection
- Registering and resolving services
- Interface-based design

**Exercises:**
1. Convert tightly-coupled code to use DI
2. Create a logging service with DI
3. Build a simple repository pattern with DI

### DILifetimes
**What you'll learn:**
- Transient: New instance every time
- Scoped: One instance per scope (HTTP request)
- Singleton: Single instance for application lifetime
- Common pitfalls (captive dependencies)

**Exercises:**
1. Observe lifetime behaviors with logging
2. Identify and fix captive dependency issues
3. Choose appropriate lifetimes for different scenarios

### AdvancedDI
**What you'll learn:**
- Factory patterns with DI
- Decorator pattern implementation
- Named/keyed services
- Configuration binding
- Service provider scopes

**Exercises:**
1. Implement a factory for runtime strategy selection
2. Create a decorator chain for cross-cutting concerns
3. Build a plugin system with DI

## Key Concepts

### Why Dependency Injection?
```csharp
// Tight coupling (BAD)
public class OrderService
{
    private readonly SqlOrderRepository _repository = new SqlOrderRepository();
}

// Loose coupling with DI (GOOD)
public class OrderService
{
    private readonly IOrderRepository _repository;

    public OrderService(IOrderRepository repository)
    {
        _repository = repository;
    }
}
```

### Service Lifetimes
- **Transient**: Use for lightweight, stateless services
- **Scoped**: Use for per-request services (e.g., DbContext)
- **Singleton**: Use for stateless services shared across the app

### Best Practices
1. Depend on abstractions, not implementations
2. Use constructor injection as default
3. Avoid service locator anti-pattern
4. Be aware of captive dependencies
5. Keep constructors simple (no logic)

## Prerequisites
- Basic C# knowledge
- Understanding of interfaces
- Familiarity with SOLID principles (helpful)

## Getting Started
Start with [BasicDI](BasicDI/) and progress sequentially through the projects.

## Next Module
After completing this module, proceed to [02-AsynchronousProcessing](../02-AsynchronousProcessing/)
