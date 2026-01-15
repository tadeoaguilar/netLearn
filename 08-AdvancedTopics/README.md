# Module 8: Advanced Topics

## Overview
Master advanced architectural concepts that separate good architects from great ones. Dive deep into Domain-Driven Design, Event Sourcing, and building resilient systems.

## Learning Objectives
- Apply strategic and tactical DDD patterns
- Implement event-sourced systems
- Build resilient, fault-tolerant applications
- Model complex business domains
- Handle distributed system failures gracefully

## Projects

### DomainDrivenDesign
**What you'll learn:**
- Strategic DDD: Bounded contexts, context mapping
- Tactical DDD: Aggregates, entities, value objects
- Domain events
- Ubiquitous language
- Anti-corruption layers

**Exercises:**
1. Define bounded contexts for e-commerce
2. Design aggregates with invariants
3. Implement domain events
4. Create context map
5. Build anti-corruption layer

### EventSourcing
**What you'll learn:**
- Event sourcing fundamentals
- Event store implementation
- Rebuilding state from events
- Snapshots for performance
- Projections and read models

**Exercises:**
1. Build event-sourced aggregate
2. Implement event store
3. Create snapshots
4. Build projections
5. Handle schema evolution

### Resilience
**What you'll learn:**
- Resilience patterns with Polly
- Circuit breaker pattern
- Retry policies
- Timeout handling
- Bulkhead isolation
- Fallback strategies

**Exercises:**
1. Implement circuit breaker
2. Add retry with exponential backoff
3. Combine multiple policies
4. Build fallback mechanism
5. Monitor resilience metrics

## Domain-Driven Design

### Strategic Design

#### Bounded Contexts
Explicit boundaries within which a model is defined:

```
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│   Sales Context  │    │ Shipping Context │    │ Billing Context  │
│                  │    │                  │    │                  │
│  - Order         │───▶│  - Shipment      │───▶│  - Invoice       │
│  - Customer      │    │  - Package       │    │  - Payment       │
│  - Product       │    │  - Delivery      │    │  - Receipt       │
└──────────────────┘    └──────────────────┘    └──────────────────┘
```

Each context has its own model of "Order" or "Customer" optimized for its needs.

#### Context Mapping
Relationships between bounded contexts:

1. **Shared Kernel**: Shared code between contexts
2. **Customer-Supplier**: Downstream depends on upstream
3. **Conformist**: Downstream conforms to upstream model
4. **Anti-Corruption Layer**: Translate between contexts
5. **Open Host Service**: Well-defined API for integration
6. **Published Language**: Shared language for integration

```csharp
// Anti-Corruption Layer
public class SalesOrderToShippingAdapter
{
    public ShippingOrder Translate(SalesOrder salesOrder)
    {
        // Translate from Sales context model to Shipping context model
        return new ShippingOrder
        {
            ShipmentId = salesOrder.OrderId,
            Items = salesOrder.Items.Select(i => new ShippingItem
            {
                Sku = i.ProductCode,
                Quantity = i.Quantity,
                Weight = i.Weight
            }).ToList(),
            Destination = new Address
            {
                Street = salesOrder.Customer.ShippingAddress.Street,
                City = salesOrder.Customer.ShippingAddress.City
            }
        };
    }
}
```

### Tactical Design

#### Entities
Objects with identity that persists over time:

```csharp
public class Order : Entity
{
    public Guid Id { get; private set; }
    public CustomerId CustomerId { get; private set; }
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }

    private Order() { } // EF

    public Order(CustomerId customerId)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Status = OrderStatus.Draft;
    }

    public void AddLine(ProductId productId, int quantity, Money unitPrice)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Cannot modify non-draft order");

        var line = new OrderLine(productId, quantity, unitPrice);
        _lines.Add(line);
        RecalculateTotal();
    }

    public void Submit()
    {
        if (!_lines.Any())
            throw new DomainException("Cannot submit empty order");

        Status = OrderStatus.Submitted;
        AddDomainEvent(new OrderSubmittedEvent(Id, CustomerId, TotalAmount));
    }

    private void RecalculateTotal()
    {
        TotalAmount = _lines.Sum(l => l.Subtotal);
    }
}
```

#### Value Objects
Immutable objects without identity, defined by their attributes:

```csharp
public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative");

        Amount = amount;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add different currencies");

        return new Money(Amount + other.Amount, Currency);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public static Money operator +(Money left, Money right) => left.Add(right);
}

public class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(string street, string city, string postalCode, string country)
    {
        Street = street ?? throw new ArgumentNullException(nameof(street));
        City = city ?? throw new ArgumentNullException(nameof(city));
        PostalCode = postalCode ?? throw new ArgumentNullException(nameof(postalCode));
        Country = country ?? throw new ArgumentNullException(nameof(country));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return PostalCode;
        yield return Country;
    }
}
```

#### Aggregates
Cluster of entities and value objects with defined boundaries:

```csharp
// Order is the Aggregate Root
public class Order : AggregateRoot
{
    // Only Order can be retrieved by repository
    // OrderLines are always accessed through Order

    private readonly List<OrderLine> _lines = new();

    // Enforce invariants
    public void AddLine(ProductId productId, int quantity, Money unitPrice)
    {
        // Business rule: Max 10 lines per order
        if (_lines.Count >= 10)
            throw new DomainException("Order cannot have more than 10 lines");

        // Business rule: Cannot duplicate products
        if (_lines.Any(l => l.ProductId == productId))
            throw new DomainException("Product already in order");

        _lines.Add(new OrderLine(productId, quantity, unitPrice));
    }

    // All modifications go through the aggregate root
    public void RemoveLine(ProductId productId)
    {
        var line = _lines.FirstOrDefault(l => l.ProductId == productId);
        if (line != null)
        {
            _lines.Remove(line);
        }
    }
}

// Repository works only with aggregate roots
public interface IOrderRepository
{
    Task<Order> GetByIdAsync(Guid id);
    Task AddAsync(Order order);
    Task UpdateAsync(Order order);
    // No IOrderLineRepository!
}
```

#### Domain Events
Something that happened in the domain that domain experts care about:

```csharp
public record OrderSubmittedEvent(
    Guid OrderId,
    CustomerId CustomerId,
    Money TotalAmount,
    DateTime SubmittedAt
) : IDomainEvent;

public class Order : AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void Submit()
    {
        Status = OrderStatus.Submitted;
        _domainEvents.Add(new OrderSubmittedEvent(
            Id, CustomerId, TotalAmount, DateTime.UtcNow));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

// Publishing domain events
public class DomainEventDispatcher
{
    private readonly IMediator _mediator;

    public async Task DispatchEventsAsync(AggregateRoot aggregate)
    {
        foreach (var domainEvent in aggregate.DomainEvents)
        {
            await _mediator.Publish(domainEvent);
        }
        aggregate.ClearDomainEvents();
    }
}
```

## Event Sourcing

### Fundamentals
Store state changes as a sequence of events instead of current state:

```csharp
// Traditional: Store current state
public class Order
{
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; } // Current state only
}

// Event Sourcing: Store events
public class Order : EventSourcedAggregate
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }

    // Rebuild state from events
    public void LoadFromHistory(IEnumerable<IEvent> events)
    {
        foreach (var @event in events)
        {
            Apply(@event, isNew: false);
        }
    }

    // Apply event
    private void Apply(IEvent @event, bool isNew)
    {
        switch (@event)
        {
            case OrderCreatedEvent e:
                Id = e.OrderId;
                Status = OrderStatus.Draft;
                break;
            case OrderSubmittedEvent e:
                Status = OrderStatus.Submitted;
                break;
            case OrderCompletedEvent e:
                Status = OrderStatus.Completed;
                break;
        }

        if (isNew)
            _uncommittedEvents.Add(@event);
    }

    // Public methods create events
    public void Submit()
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Only draft orders can be submitted");

        Apply(new OrderSubmittedEvent(Id, DateTime.UtcNow), isNew: true);
    }
}
```

### Event Store

```csharp
public interface IEventStore
{
    Task<IEnumerable<IEvent>> GetEventsAsync(Guid aggregateId);
    Task SaveEventsAsync(Guid aggregateId, IEnumerable<IEvent> events, int expectedVersion);
}

public class EventStore : IEventStore
{
    private readonly DbContext _context;

    public async Task<IEnumerable<IEvent>> GetEventsAsync(Guid aggregateId)
    {
        var eventRecords = await _context.Events
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Version)
            .ToListAsync();

        return eventRecords.Select(e => DeserializeEvent(e.EventType, e.EventData));
    }

    public async Task SaveEventsAsync(Guid aggregateId,
        IEnumerable<IEvent> events, int expectedVersion)
    {
        // Optimistic concurrency
        var currentVersion = await _context.Events
            .Where(e => e.AggregateId == aggregateId)
            .MaxAsync(e => (int?)e.Version) ?? 0;

        if (currentVersion != expectedVersion)
            throw new ConcurrencyException();

        var version = expectedVersion;
        foreach (var @event in events)
        {
            version++;
            var eventRecord = new EventRecord
            {
                Id = Guid.NewGuid(),
                AggregateId = aggregateId,
                Version = version,
                EventType = @event.GetType().Name,
                EventData = JsonSerializer.Serialize(@event),
                CreatedAt = DateTime.UtcNow
            };
            _context.Events.Add(eventRecord);
        }

        await _context.SaveChangesAsync();
    }
}
```

### Snapshots
Optimize performance for long event streams:

```csharp
public class SnapshotStore
{
    public async Task<Snapshot> GetSnapshotAsync(Guid aggregateId)
    {
        return await _context.Snapshots
            .Where(s => s.AggregateId == aggregateId)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync();
    }

    public async Task SaveSnapshotAsync(Snapshot snapshot)
    {
        _context.Snapshots.Add(snapshot);
        await _context.SaveChangesAsync();
    }
}

// Loading with snapshot
public async Task<Order> GetByIdAsync(Guid id)
{
    var snapshot = await _snapshotStore.GetSnapshotAsync(id);
    var order = snapshot != null
        ? RestoreFromSnapshot(snapshot)
        : new Order();

    var events = await _eventStore.GetEventsAsync(id,
        fromVersion: snapshot?.Version ?? 0);
    order.LoadFromHistory(events);

    return order;
}
```

### Projections
Build read models from events:

```csharp
public class OrderProjection : IEventHandler<OrderCreatedEvent>,
                                IEventHandler<OrderSubmittedEvent>
{
    private readonly IReadModelRepository _readModel;

    public async Task Handle(OrderCreatedEvent @event)
    {
        var orderView = new OrderView
        {
            Id = @event.OrderId,
            CustomerId = @event.CustomerId,
            Status = "Draft",
            CreatedAt = @event.CreatedAt
        };
        await _readModel.InsertAsync(orderView);
    }

    public async Task Handle(OrderSubmittedEvent @event)
    {
        var orderView = await _readModel.GetAsync(@event.OrderId);
        orderView.Status = "Submitted";
        orderView.SubmittedAt = @event.SubmittedAt;
        await _readModel.UpdateAsync(orderView);
    }
}
```

## Resilience Patterns

### Polly Library

#### Circuit Breaker
```csharp
var circuitBreakerPolicy = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 3,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (exception, duration) =>
        {
            _logger.LogWarning("Circuit breaker opened");
        },
        onReset: () =>
        {
            _logger.LogInformation("Circuit breaker reset");
        });

// Usage
await circuitBreakerPolicy.ExecuteAsync(async () =>
{
    return await _httpClient.GetAsync(url);
});
```

#### Retry with Exponential Backoff
```csharp
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            _logger.LogWarning("Retry {RetryCount} after {Delay}ms",
                retryCount, timeSpan.TotalMilliseconds);
        });
```

#### Timeout
```csharp
var timeoutPolicy = Policy
    .TimeoutAsync(TimeSpan.FromSeconds(10),
        onTimeoutAsync: async (context, timespan, task) =>
        {
            _logger.LogWarning("Timeout after {Timeout}s", timespan.TotalSeconds);
        });
```

#### Combining Policies
```csharp
var policyWrap = Policy.WrapAsync(
    fallbackPolicy,    // Outermost
    circuitBreakerPolicy,
    retryPolicy,
    timeoutPolicy      // Innermost
);

await policyWrap.ExecuteAsync(async () =>
{
    return await _externalService.CallAsync();
});
```

#### Bulkhead Isolation
```csharp
var bulkheadPolicy = Policy
    .BulkheadAsync(
        maxParallelization: 10,
        maxQueuingActions: 20,
        onBulkheadRejectedAsync: async context =>
        {
            _logger.LogWarning("Bulkhead rejected");
        });
```

## Best Practices

### Domain-Driven Design
1. Collaborate with domain experts
2. Use ubiquitous language everywhere
3. Define clear bounded context boundaries
4. Keep aggregates small
5. Protect invariants within aggregates
6. Use value objects for concepts without identity

### Event Sourcing
1. Events are immutable facts
2. Never delete events
3. Use snapshots for performance
4. Version your events
5. Handle event schema evolution
6. Rebuild projections as needed

### Resilience
1. Fail fast when appropriate
2. Use timeouts on all external calls
3. Implement circuit breakers for remote services
4. Log all policy actions
5. Monitor policy metrics
6. Test failure scenarios

## Exercises

### Exercise 1: DDD E-Commerce
Build order management with:
- Multiple bounded contexts
- Rich domain model
- Aggregates with invariants
- Domain events
- Anti-corruption layer

### Exercise 2: Event-Sourced System
Create event-sourced banking:
- Account aggregate
- Event store
- Snapshots every 100 events
- Multiple projections
- Event schema versioning

### Exercise 3: Resilient Service
Build resilient API client:
- Retry with exponential backoff
- Circuit breaker
- Timeout policies
- Fallback mechanisms
- Metrics and monitoring

### Exercise 4: Complete System
Combine all concepts:
- DDD with bounded contexts
- Event-sourced aggregates
- CQRS with projections
- Resilient communication
- Saga for workflows

## Prerequisites
- All previous modules completed
- Strong understanding of distributed systems
- Database transaction knowledge
- Experience with complex business domains

## Getting Started
Work through in this order:
1. [DomainDrivenDesign](DomainDrivenDesign/)
2. [EventSourcing](EventSourcing/)
3. [Resilience](Resilience/)

## Recommended Reading
- "Domain-Driven Design" by Eric Evans
- "Implementing Domain-Driven Design" by Vaughn Vernon
- "Event Sourcing" by Martin Fowler
- Polly documentation

## Congratulations!
You've completed the .NET Learning Path for Architects. Now build real-world projects applying these concepts!
