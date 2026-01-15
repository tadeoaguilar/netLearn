# Module 4: Vertical Slice Architecture

## Overview
Vertical Slice Architecture organizes code by features rather than technical layers. Each feature (slice) contains all the code needed for that specific functionality, promoting cohesion and reducing coupling.

## Learning Objectives
- Understand feature-based organization
- Compare vertical slices with layered architecture
- Implement features with MediatR
- Reduce coupling between features
- Balance feature autonomy with code reuse

## Architectural Philosophy

### Traditional Layered (Horizontal)
```
Controllers/
├── UsersController.cs
├── OrdersController.cs
├── ProductsController.cs
Services/
├── UserService.cs
├── OrderService.cs
├── ProductService.cs
Repositories/
├── UserRepository.cs
├── OrderRepository.cs
├── ProductRepository.cs
```
Changes to a feature touch multiple layers.

### Vertical Slice (Feature-based)
```
Features/
├── Users/
│   ├── Create/
│   │   ├── CreateUserCommand.cs
│   │   ├── CreateUserHandler.cs
│   │   ├── CreateUserValidator.cs
│   │   └── CreateUserEndpoint.cs
│   ├── GetById/
│   └── Update/
├── Orders/
│   ├── PlaceOrder/
│   ├── CancelOrder/
│   └── GetOrderHistory/
```
Each feature is self-contained.

## Project Structure

### VerticalSliceDemo
Complete implementation with:
- Feature folders organization
- MediatR for request handling
- FluentValidation for validation
- Minimal shared code
- Comparison with traditional approach

## Key Concepts

### Feature Slice Components
Each slice typically contains:
1. **Request/Command**: Input data
2. **Handler**: Business logic
3. **Validator**: Input validation
4. **Endpoint**: API endpoint
5. **Query/Repository**: Data access (if needed)

```csharp
// Features/Orders/PlaceOrder/PlaceOrderCommand.cs
public record PlaceOrderCommand(Guid CustomerId, List<OrderItem> Items)
    : IRequest<OrderDto>;

// PlaceOrderHandler.cs
public class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, OrderDto>
{
    private readonly ApplicationDbContext _context;

    public async Task<OrderDto> Handle(PlaceOrderCommand request,
        CancellationToken cancellationToken)
    {
        // All logic for placing an order is here
        var order = new Order
        {
            CustomerId = request.CustomerId,
            Items = request.Items
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }
}

// PlaceOrderValidator.cs
public class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
    }
}
```

### MediatR Integration
MediatR decouples requests from handlers:
```csharp
// Controller becomes thin
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    [HttpPost]
    public async Task<IActionResult> PlaceOrder(PlaceOrderCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
```

## Benefits

### 1. High Cohesion
- Related code stays together
- Easy to understand feature scope
- Changes are localized

### 2. Low Coupling
- Features don't depend on each other
- Shared code is minimized
- Teams can work independently

### 3. Easy to Navigate
- Find all code for a feature in one place
- No jumping between layers
- New developers onboard faster

### 4. Flexible Implementation
- Each slice can use different approaches
- Optimize per feature needs
- No forced consistency

### 5. Scalability
- Features can be extracted to microservices easily
- Team ownership per feature
- Independent deployment (with proper boundaries)

## Trade-offs

### Pros
- Faster feature development
- Easier to understand and modify
- Better team autonomy
- Reduced merge conflicts

### Cons
- Code duplication possible
- Less architectural consistency
- Harder to enforce patterns
- May need shared kernel for common logic

## Shared Code Strategy

### Common Folder
For truly shared concerns:
```
Common/
├── Database/
│   └── ApplicationDbContext.cs
├── Behaviors/
│   ├── ValidationBehavior.cs
│   └── LoggingBehavior.cs
├── Extensions/
└── Abstractions/
```

### Guidelines
1. Share only when necessary
2. Prefer duplication over wrong abstraction
3. Extract to shared only after 3+ uses
4. Keep shared code stable

## When to Use Vertical Slices

### Good Fit
- Complex applications with many features
- Feature-focused teams
- Microservices preparation
- Frequently changing requirements

### Not Ideal
- Simple CRUD applications
- Heavy domain logic requiring consistency
- Small applications
- Strict architectural governance needed

## Exercises

1. **Convert Layered to Vertical**
   - Take a layered application
   - Reorganize by features
   - Measure coupling improvements

2. **Build Feature-First**
   - Shopping cart feature
   - User authentication feature
   - Product catalog feature
   - Keep features independent

3. **Refactor Shared Code**
   - Identify duplicate code
   - Decide: keep duplicate or share?
   - Extract to shared kernel if needed

4. **Compare Approaches**
   - Add same feature to both architectures
   - Measure: files touched, time taken
   - Analyze maintainability

## MediatR Pipeline Behaviors

### Cross-Cutting Concerns
```csharp
// Validation Behavior
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Validate before handler
        var failures = _validators
            .Select(v => v.Validate(request))
            .SelectMany(result => result.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
            throw new ValidationException(failures);

        return await next();
    }
}
```

## Comparison with Clean Architecture

| Aspect | Vertical Slice | Clean Architecture |
|--------|----------------|-------------------|
| Organization | By feature | By layer |
| Coupling | Between features | Between layers |
| Flexibility | High per feature | High per layer |
| Consistency | Lower | Higher |
| Team scaling | Better | Good |
| DDD fit | Medium | Excellent |

## Best Practices
1. Keep features truly independent
2. Use MediatR for request handling
3. Validate at the feature boundary
4. Minimize shared code initially
5. Consider feature flags for deployment
6. Document feature dependencies
7. Use integration tests per feature

## Prerequisites
- Module 3 (Clean Architecture) completed
- Understanding of CQRS basics
- Familiarity with MediatR

## Getting Started
Explore [VerticalSliceDemo](VerticalSliceDemo/) and compare with Clean Architecture from Module 3.

## Next Module
After completing this module, proceed to [05-DistributedSystems](../05-DistributedSystems/)
