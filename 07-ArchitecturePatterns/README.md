# Module 7: Architecture Patterns

## Overview
Master essential architecture patterns used in enterprise applications. Learn when and how to apply patterns like CQRS, Repository, Saga, and Outbox to solve common architectural challenges.

## Learning Objectives
- Understand the purpose of each pattern
- Know when to apply each pattern
- Implement patterns correctly in .NET
- Recognize trade-offs and alternatives
- Combine patterns effectively

## Projects

### CQRS-MediatR
**What you'll learn:**
- Full CQRS implementation with MediatR
- FluentValidation for command validation
- Pipeline behaviors for cross-cutting concerns
- Separate read and write models
- Query optimization strategies

**Exercises:**
1. Build CQRS-based blog system
2. Add validation pipeline
3. Implement caching for queries
4. Add logging and performance tracking

### Repository-UnitOfWork
**What you'll learn:**
- Repository pattern for data access
- Unit of Work pattern for transaction management
- Generic vs specific repositories
- Abstraction over ORM
- Testing with repositories

**Exercises:**
1. Implement generic repository
2. Create Unit of Work coordinator
3. Build specific repositories
4. Mock repositories for unit testing

### Saga
**What you'll learn:**
- Distributed transaction patterns
- Orchestration-based sagas
- Choreography-based sagas
- Compensation logic
- State management

**Exercises:**
1. Build order processing saga
2. Implement compensation actions
3. Handle partial failures
4. Add saga state persistence

### Outbox
**What you'll learn:**
- Transactional outbox pattern
- Reliable message publishing
- Outbox processor
- Idempotency handling
- Inbox pattern (receiver side)

**Exercises:**
1. Implement outbox table
2. Create background processor
3. Ensure exactly-once delivery
4. Build inbox for consumers

## Pattern Details

### 1. CQRS Pattern

#### Purpose
Separate read and write operations to optimize for different concerns.

#### Structure
```csharp
// Command (Write) - Changes state
public record CreateProductCommand(string Name, decimal Price)
    : IRequest<Guid>;

public class CreateProductHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly AppDbContext _context;

    public async Task<Guid> Handle(CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Price = request.Price
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}

// Query (Read) - Returns data
public record GetProductQuery(Guid Id) : IRequest<ProductDto>;

public class GetProductHandler : IRequestHandler<GetProductQuery, ProductDto>
{
    private readonly IReadOnlyDbContext _readContext;

    public async Task<ProductDto> Handle(GetProductQuery request,
        CancellationToken cancellationToken)
    {
        return await _readContext.Products
            .Where(p => p.Id == request.Id)
            .Select(p => new ProductDto(p.Id, p.Name, p.Price))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

#### When to Use
- Complex domain with different read/write patterns
- High read-to-write ratio
- Need for different data models
- Scalability requirements

#### Benefits
- Optimized queries
- Simplified commands
- Independent scaling
- Clear intent

#### Trade-offs
- Eventual consistency
- More complex infrastructure
- Code duplication

### 2. Repository Pattern

#### Purpose
Abstract data access logic and provide a collection-like interface.

#### Structure
```csharp
// Generic repository interface
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}

// Implementation
public class Repository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    // ... other implementations
}

// Specific repository
public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetByCategory(string category);
    Task<IEnumerable<Product>> GetLowStockProducts(int threshold);
}
```

#### When to Use
- Need testable data access
- Multiple data sources
- Want to hide ORM details
- Complex query logic

#### Benefits
- Testability
- Centralized data access logic
- Abstraction from ORM
- Reusable queries

#### Trade-offs
- Extra abstraction layer
- Can become anemic
- May hide ORM features

### 3. Unit of Work Pattern

#### Purpose
Maintain a list of objects affected by a business transaction and coordinate changes.

#### Structure
```csharp
public interface IUnitOfWork : IDisposable
{
    IProductRepository Products { get; }
    IOrderRepository Orders { get; }
    ICustomerRepository Customers { get; }

    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction _transaction;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Products = new ProductRepository(context);
        Orders = new OrderRepository(context);
        Customers = new CustomerRepository(context);
    }

    public IProductRepository Products { get; }
    public IOrderRepository Orders { get; }
    public ICustomerRepository Customers { get; }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        await _transaction.CommitAsync();
    }

    // Usage
    public async Task PlaceOrder(Order order)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.Orders.AddAsync(order);
            await _uow.Products.UpdateStockAsync(order.Items);
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }
}
```

#### When to Use
- Multiple repository operations in single transaction
- Need transaction control
- Coordinating changes across aggregates

#### Benefits
- Transaction management
- Consistency across repositories
- Clean API

#### Trade-offs
- Adds complexity
- May encourage anemic domain models
- EF Core DbContext already implements this

### 4. Saga Pattern

#### Purpose
Manage distributed transactions across multiple services.

#### Orchestration-Based
```csharp
public class OrderSaga
{
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly IInventoryService _inventoryService;
    private readonly ISagaStateRepository _stateRepo;

    public async Task ExecuteAsync(CreateOrderCommand command)
    {
        var state = new OrderSagaState { OrderId = Guid.NewGuid() };

        try
        {
            // Step 1: Create order
            state.OrderCreated = true;
            await _orderService.CreateOrderAsync(state.OrderId, command);
            await _stateRepo.SaveAsync(state);

            // Step 2: Process payment
            state.PaymentProcessed = true;
            await _paymentService.ProcessPaymentAsync(state.OrderId, command.Amount);
            await _stateRepo.SaveAsync(state);

            // Step 3: Reserve inventory
            state.InventoryReserved = true;
            await _inventoryService.ReserveItemsAsync(state.OrderId, command.Items);
            await _stateRepo.SaveAsync(state);

            // Mark complete
            state.Status = SagaStatus.Completed;
            await _stateRepo.SaveAsync(state);
        }
        catch (Exception ex)
        {
            // Compensate in reverse order
            await CompensateAsync(state);
            state.Status = SagaStatus.Failed;
            await _stateRepo.SaveAsync(state);
            throw;
        }
    }

    private async Task CompensateAsync(OrderSagaState state)
    {
        if (state.InventoryReserved)
            await _inventoryService.ReleaseItemsAsync(state.OrderId);

        if (state.PaymentProcessed)
            await _paymentService.RefundPaymentAsync(state.OrderId);

        if (state.OrderCreated)
            await _orderService.CancelOrderAsync(state.OrderId);
    }
}
```

#### Choreography-Based
```csharp
// Order Service
public async Task PlaceOrder(Order order)
{
    await _orderRepo.SaveAsync(order);
    await _bus.PublishAsync(new OrderPlacedEvent(order));
}

// Payment Service
public class OrderPlacedHandler : IEventHandler<OrderPlacedEvent>
{
    public async Task Handle(OrderPlacedEvent @event)
    {
        try
        {
            await ProcessPayment(@event.OrderId, @event.Amount);
            await _bus.PublishAsync(new PaymentProcessedEvent(@event.OrderId));
        }
        catch
        {
            await _bus.PublishAsync(new PaymentFailedEvent(@event.OrderId));
        }
    }
}

// Inventory Service
public class PaymentProcessedHandler : IEventHandler<PaymentProcessedEvent>
{
    public async Task Handle(PaymentProcessedEvent @event)
    {
        await ReserveInventory(@event.OrderId);
        await _bus.PublishAsync(new InventoryReservedEvent(@event.OrderId));
    }
}
```

#### When to Use
- Distributed transactions needed
- Long-running business processes
- Multiple services must coordinate
- ACID not possible

### 5. Outbox Pattern

#### Purpose
Ensure reliable message publishing by using transactional outbox.

#### Structure
```csharp
// Outbox table
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

// Save entity and outbox message in same transaction
public async Task PlaceOrder(Order order)
{
    using var transaction = await _context.Database.BeginTransactionAsync();

    // Save domain entity
    _context.Orders.Add(order);

    // Save outbox message
    var outboxMessage = new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = nameof(OrderPlacedEvent),
        Payload = JsonSerializer.Serialize(new OrderPlacedEvent(order)),
        CreatedAt = DateTime.UtcNow
    };
    _context.OutboxMessages.Add(outboxMessage);

    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}

// Background processor
public class OutboxProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var messages = await _context.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .Take(100)
                .ToListAsync();

            foreach (var message in messages)
            {
                try
                {
                    // Publish to message bus
                    await _messageBus.PublishAsync(
                        message.Type,
                        message.Payload);

                    // Mark as processed
                    message.ProcessedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process outbox message {Id}",
                        message.Id);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

#### When to Use
- Must guarantee message delivery
- Need transactional consistency
- Eventual consistency acceptable
- Event-driven architecture

## Pattern Combinations

### CQRS + Outbox + Saga
```csharp
// Command creates order and publishes event via outbox
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        var order = new Order(request);
        _context.Orders.Add(order);

        // Add to outbox
        _context.OutboxMessages.Add(new OutboxMessage(
            new OrderCreatedEvent(order)));

        await _context.SaveChangesAsync(cancellationToken);

        // Saga orchestrator picks up the event
        return order.Id;
    }
}
```

## Best Practices

### General
1. Don't over-engineer
2. Start simple, add patterns as needed
3. Understand the trade-offs
4. Document pattern usage
5. Be consistent across the codebase

### CQRS
1. Keep commands simple
2. Optimize queries independently
3. Handle eventual consistency
4. Version your commands/queries

### Repository
1. Avoid generic repositories for everything
2. Create specific repositories for complex queries
3. Don't expose IQueryable
4. Keep repositories focused

### Saga
1. Make steps idempotent
2. Always implement compensation
3. Persist saga state
4. Monitor saga execution

### Outbox
1. Process messages in background
2. Handle duplicates
3. Monitor outbox table size
4. Add retry logic

## Exercises

### Exercise 1: Full CQRS
Build product catalog with:
- Commands: Create, Update, Delete
- Queries: GetById, Search, GetByCategory
- Validation pipeline
- Caching for queries

### Exercise 2: Repository Pattern
Implement complete repository system:
- Generic base repository
- Specific repositories with custom queries
- Unit of Work coordinator
- Unit tests with mocks

### Exercise 3: Order Saga
Create distributed order processing:
- Order creation
- Payment processing
- Inventory reservation
- Shipping notification
- Full compensation logic

### Exercise 4: Outbox Implementation
Build reliable messaging:
- Outbox pattern for publishing
- Background processor
- Inbox pattern for receiving
- Idempotency handling

## Prerequisites
- Modules 1-6 completed
- Understanding of database transactions
- Familiarity with messaging concepts
- Knowledge of distributed systems basics

## Getting Started
Work through projects in this order:
1. [CQRS-MediatR](CQRS-MediatR/)
2. [Repository-UnitOfWork](Repository-UnitOfWork/)
3. [Saga](Saga/)
4. [Outbox](Outbox/)

## Next Module
After completing this module, proceed to [08-AdvancedTopics](../08-AdvancedTopics/)
