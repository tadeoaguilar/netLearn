# Module 3: Clean Architecture

## Overview
Clean Architecture, popularized by Robert C. Martin (Uncle Bob), creates a maintainable, testable system with clear separation of concerns. Learn to structure applications where business logic is independent of frameworks, UI, and databases.

## Learning Objectives
- Understand Clean Architecture principles
- Implement proper layering and dependency flow
- Apply Dependency Inversion Principle
- Create testable, maintainable applications
- Separate business rules from infrastructure

## Project Structure

### CleanArchitectureDemo
A complete implementation demonstrating all layers:

```
CleanArchitectureDemo/
├── Domain/                 # Enterprise business rules
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Enums/
│   └── Exceptions/
├── Application/            # Application business rules
│   ├── Interfaces/
│   ├── DTOs/
│   ├── UseCases/
│   └── Mappings/
├── Infrastructure/         # External concerns
│   ├── Persistence/
│   ├── ExternalServices/
│   └── Configuration/
└── WebAPI/                # Presentation layer
    ├── Controllers/
    ├── Filters/
    └── Middleware/
```

## The Layers

### 1. Domain Layer (Core)
**Responsibility**: Enterprise-wide business rules
- Entities with business logic
- Value objects
- Domain events
- Domain exceptions
- **Dependencies**: NONE (pure C#)

```csharp
public class Order
{
    public Guid Id { get; private set; }
    public decimal TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }

    public void Complete()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be completed");

        Status = OrderStatus.Completed;
    }
}
```

### 2. Application Layer
**Responsibility**: Application-specific business rules
- Use cases / command handlers
- Interfaces (ports)
- DTOs / View models
- Validation logic
- **Dependencies**: Domain layer only

```csharp
public interface IOrderRepository
{
    Task<Order> GetByIdAsync(Guid id);
    Task AddAsync(Order order);
}

public class CompleteOrderUseCase
{
    private readonly IOrderRepository _repository;

    public async Task ExecuteAsync(Guid orderId)
    {
        var order = await _repository.GetByIdAsync(orderId);
        order.Complete();
        await _repository.UpdateAsync(order);
    }
}
```

### 3. Infrastructure Layer
**Responsibility**: External concerns and implementations
- Database access (EF Core)
- External APIs
- File system
- Email/SMS services
- **Dependencies**: Application, Domain

```csharp
public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;

    public async Task<Order> GetByIdAsync(Guid id)
    {
        return await _context.Orders.FindAsync(id);
    }
}
```

### 4. Presentation Layer (WebAPI/UI)
**Responsibility**: User interaction
- Controllers / Pages
- Request/Response models
- Middleware
- Dependency injection setup
- **Dependencies**: Application, Infrastructure (for DI only)

## Key Principles

### Dependency Rule
**Dependencies only point inward**
- Outer layers depend on inner layers
- Inner layers know nothing about outer layers
- Domain has zero dependencies

### Benefits
1. **Independent of Frameworks**: Business logic doesn't depend on EF, ASP.NET, etc.
2. **Testable**: Business rules can be tested without UI, database, or external services
3. **Independent of UI**: Can change UI without affecting business rules
4. **Independent of Database**: Can swap SQL Server for MongoDB without changing business logic
5. **Independent of External Services**: Business rules don't know about external services

### Trade-offs
- More files and abstractions
- Steeper learning curve
- May be overkill for simple CRUD apps
- Requires discipline to maintain boundaries

## Exercises

1. **Build a Task Management System**
   - Domain: Task, Project entities
   - Use cases: CreateTask, AssignTask, CompleteTask
   - Persistence: EF Core with SQLite
   - API: REST endpoints

2. **Add Features While Maintaining Clean Architecture**
   - Add email notifications (Infrastructure)
   - Add search functionality (Application)
   - Add validation rules (Domain)

3. **Swap Implementations**
   - Replace EF Core with Dapper
   - Add in-memory repository for testing
   - Add multiple UI layers (API + Blazor)

4. **Testing**
   - Unit test domain logic (no dependencies)
   - Test use cases with mock repositories
   - Integration tests with real database

## Common Patterns Used
- Repository Pattern
- Unit of Work
- Dependency Injection
- CQRS (optional)
- Mediator Pattern (optional)

## Best Practices
1. Keep Domain layer pure (no dependencies)
2. Define interfaces in Application layer
3. Implement interfaces in Infrastructure layer
4. Use DTOs to cross boundaries
5. Validate at boundaries (API and Domain)
6. Keep use cases focused (Single Responsibility)

## Comparison with Other Architectures

| Aspect | Clean | Layered | Onion | Hexagonal |
|--------|-------|---------|-------|-----------|
| Core Focus | Use Cases | Data Layer | Domain | Ports & Adapters |
| Complexity | Medium | Low | Medium | Medium |
| Testability | Excellent | Good | Excellent | Excellent |
| Flexibility | High | Medium | High | High |

## Prerequisites
- Modules 1 & 2 completed
- Understanding of SOLID principles
- Familiarity with repository pattern

## Getting Started
Explore [CleanArchitectureDemo](CleanArchitectureDemo/) and build the exercises step by step.

## Next Module
After completing this module, proceed to [04-VerticalSliceArchitecture](../04-VerticalSliceArchitecture/) to see an alternative approach.
