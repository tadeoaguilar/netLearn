# Module 5: Distributed Systems

## Overview
Learn to build resilient, scalable distributed applications. Master message brokers, event-driven architecture, and CQRS patterns essential for modern distributed systems.

## Learning Objectives
- Understand distributed system challenges
- Implement message-based communication
- Design event-driven architectures
- Apply CQRS pattern
- Handle eventual consistency
- Manage distributed transactions

## Projects

### MessageQueues
**What you'll learn:**
- Message queue fundamentals
- RabbitMQ integration
- Azure Service Bus usage
- Message patterns: pub/sub, point-to-point
- Dead letter queues
- Message retry policies

**Exercises:**
1. Build order processing with RabbitMQ
2. Implement competing consumers
3. Handle message failures and retries
4. Create topic-based routing

### EventDriven
**What you'll learn:**
- Event-driven architecture principles
- Domain events vs integration events
- Event publishing and subscription
- Event store basics
- Saga pattern for workflows

**Exercises:**
1. Design event-driven e-commerce flow
2. Implement event handlers
3. Build choreography-based saga
4. Handle compensating transactions

### CQRS
**What you'll learn:**
- Command Query Responsibility Segregation
- Separate read and write models
- Eventual consistency handling
- Projection patterns
- Event sourcing basics

**Exercises:**
1. Implement CQRS for product catalog
2. Build separate read/write databases
3. Create projections from events
4. Handle synchronization lag

## Key Concepts

### Distributed System Challenges

#### CAP Theorem
- **Consistency**: All nodes see the same data
- **Availability**: System remains operational
- **Partition Tolerance**: System works despite network issues
- **Reality**: Can only guarantee 2 of 3

#### Common Problems
1. Network reliability
2. Latency
3. Partial failures
4. Data consistency
5. Clock synchronization

### Message Queues

#### Why Use Message Queues?
```csharp
// Without queue - tight coupling
public async Task PlaceOrder(Order order)
{
    await _orderRepo.SaveAsync(order);
    await _paymentService.ProcessPayment(order); // What if this fails?
    await _inventoryService.ReserveItems(order);
    await _emailService.SendConfirmation(order);
}

// With queue - loose coupling
public async Task PlaceOrder(Order order)
{
    await _orderRepo.SaveAsync(order);
    await _messageQueue.PublishAsync(new OrderPlacedEvent(order));
    // Other services consume the event independently
}
```

#### Message Patterns
1. **Point-to-Point**: One sender, one receiver
2. **Publish-Subscribe**: One sender, multiple receivers
3. **Request-Reply**: Synchronous over async messaging
4. **Competing Consumers**: Multiple receivers for load balancing

### Event-Driven Architecture

#### Domain Events
Internal events within a bounded context:
```csharp
public class Order
{
    private List<IDomainEvent> _events = new();

    public void Complete()
    {
        Status = OrderStatus.Completed;
        _events.Add(new OrderCompletedEvent(Id));
    }

    public IReadOnlyList<IDomainEvent> GetDomainEvents() => _events;
}
```

#### Integration Events
Events for cross-service communication:
```csharp
public record OrderPlacedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime PlacedAt
);
```

#### Event Handling
```csharp
public class OrderPlacedEventHandler : IEventHandler<OrderPlacedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IInventoryService _inventoryService;

    public async Task Handle(OrderPlacedEvent @event)
    {
        await _inventoryService.ReserveItems(@event.OrderId);
        await _emailService.SendOrderConfirmation(@event.OrderId);
    }
}
```

### CQRS Pattern

#### Separate Models
```csharp
// Write Model (Commands)
public class CreateOrderCommand
{
    public Guid CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
}

public class CreateOrderHandler
{
    public async Task<Guid> Handle(CreateOrderCommand command)
    {
        var order = new Order(command.CustomerId, command.Items);
        await _writeDb.SaveAsync(order);
        return order.Id;
    }
}

// Read Model (Queries)
public class GetOrderQuery
{
    public Guid OrderId { get; set; }
}

public class GetOrderHandler
{
    public async Task<OrderDto> Handle(GetOrderQuery query)
    {
        // Query optimized read model
        return await _readDb.Orders
            .Where(o => o.Id == query.OrderId)
            .FirstOrDefaultAsync();
    }
}
```

#### Benefits of CQRS
1. Optimized read and write models
2. Scalability (scale reads/writes independently)
3. Flexibility in data storage
4. Clear separation of concerns

#### When to Use CQRS
- Complex domain with different read/write needs
- High read vs write ratio
- Need for different data representations
- Eventual consistency is acceptable

### Eventual Consistency

#### Understanding Eventual Consistency
```csharp
// Write occurs
await _commandDb.SaveOrderAsync(order);
await _bus.PublishAsync(new OrderCreatedEvent(order));

// Read may not immediately reflect write
var orderView = await _queryDb.GetOrderAsync(order.Id);
// orderView might be null briefly
```

#### Handling Strategies
1. **User feedback**: "Processing your request..."
2. **Polling**: Check until data appears
3. **Webhooks**: Notify when ready
4. **Inbox pattern**: Guarantee message processing

## Saga Pattern

### Orchestration vs Choreography

#### Orchestration
Central coordinator manages workflow:
```csharp
public class OrderSaga
{
    public async Task ExecuteAsync(CreateOrderCommand command)
    {
        var orderId = await CreateOrder(command);
        try
        {
            await ProcessPayment(orderId);
            await ReserveInventory(orderId);
            await ShipOrder(orderId);
        }
        catch
        {
            await CompensateOrder(orderId);
            throw;
        }
    }
}
```

#### Choreography
Services react to events:
```csharp
// Order Service
orderCreated.Publish();

// Payment Service listens
OnOrderCreated => ProcessPayment => PaymentProcessed.Publish();

// Inventory Service listens
OnPaymentProcessed => ReserveItems => ItemsReserved.Publish();
```

## Best Practices

### Message Design
1. Include correlation ID for tracing
2. Make messages immutable
3. Version your messages
4. Keep messages small
5. Include timestamp

### Idempotency
Messages may be delivered multiple times:
```csharp
public class IdempotentHandler
{
    public async Task Handle(PaymentCommand command)
    {
        // Check if already processed
        if (await _processed.Contains(command.Id))
            return;

        // Process
        await ProcessPayment(command);

        // Mark as processed
        await _processed.Add(command.Id);
    }
}
```

### Error Handling
1. Use dead letter queues
2. Implement retry with exponential backoff
3. Log all failures
4. Monitor queue depths
5. Alert on poison messages

### Monitoring
- Message processing time
- Queue depth
- Failed messages
- Consumer lag
- End-to-end latency

## Anti-Patterns to Avoid
1. **Distributed monolith**: Services too coupled
2. **Chatty communication**: Too many messages
3. **Ignoring failures**: No retry or compensation
4. **Synchronous chains**: Defeating the purpose
5. **Large messages**: Use claim check pattern

## Exercises

### Exercise 1: E-Commerce Flow
Build event-driven order processing:
1. Order placement
2. Payment processing
3. Inventory reservation
4. Shipping notification
5. Email confirmation

### Exercise 2: CQRS Implementation
Create CQRS for blog system:
- Commands: CreatePost, UpdatePost, DeletePost
- Queries: GetPost, SearchPosts, GetPostsByAuthor
- Separate databases for read/write

### Exercise 3: Saga Pattern
Implement booking saga with compensation:
1. Reserve flight
2. Reserve hotel
3. Process payment
4. Compensate if any step fails

### Exercise 4: Message Resilience
Handle failures gracefully:
1. Implement retry logic
2. Use dead letter queues
3. Create poison message handler
4. Monitor and alert

## Prerequisites
- Modules 1-4 completed
- Understanding of async programming
- Basic networking knowledge
- Docker for running message brokers

## Getting Started
1. Install Docker
2. Start with [MessageQueues](MessageQueues/)
3. Progress to [EventDriven](EventDriven/)
4. Complete with [CQRS](CQRS/)

## Next Module
After completing this module, proceed to [06-CloudNative](../06-CloudNative/)
