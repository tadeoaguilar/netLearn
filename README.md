# .NET Learning Path for Software Architects

A comprehensive, hands-on learning repository designed to master .NET architecture concepts and patterns. This repository contains practical projects and examples to help you become a successful software architect.

## Learning Path Overview

This repository is organized into 8 progressive modules, each focusing on critical architectural concepts:

### 1. Dependency Injection (01-DependencyInjection/)
**Goal**: Master IoC containers and DI patterns in .NET

- **BasicDI**: Introduction to dependency injection concepts
- **DILifetimes**: Understanding Transient, Scoped, and Singleton lifetimes
- **AdvancedDI**: Factory patterns, decorators, and advanced scenarios

**Key Skills**: Service registration, lifetime management, constructor injection, interface-based design

---

### 2. Asynchronous Processing (02-AsynchronousProcessing/)
**Goal**: Build high-performance, scalable applications with async patterns

- **AsyncAwait**: Mastering async/await patterns
- **TaskParallelLibrary**: Parallel processing and task coordination
- **Channels**: Producer-consumer patterns with System.Threading.Channels

**Key Skills**: Async programming, cancellation tokens, parallelism, backpressure handling

---

### 3. Clean Architecture (03-CleanArchitecture/)
**Goal**: Implement maintainable, testable architecture with clear separation of concerns

- **CleanArchitectureDemo**: Full implementation of Clean Architecture
  - Core/Domain layer (entities, interfaces)
  - Application layer (use cases, DTOs)
  - Infrastructure layer (data access, external services)
  - Presentation layer (API/UI)

**Key Skills**: Dependency inversion, use case driven development, testability

---

### 4. Vertical Slice Architecture (04-VerticalSliceArchitecture/)
**Goal**: Organize code by features rather than technical layers

- **VerticalSliceDemo**: Feature-based organization
  - Compare with traditional layered approach
  - MediatR for request handling
  - Self-contained features

**Key Skills**: Feature folders, CQRS-lite, minimal coupling

---

### 5. Distributed Systems (05-DistributedSystems/)
**Goal**: Build resilient, scalable distributed applications

- **MessageQueues**: RabbitMQ, Azure Service Bus integration
- **EventDriven**: Event-driven architecture patterns
- **CQRS**: Command Query Responsibility Segregation

**Key Skills**: Message brokers, eventual consistency, event sourcing basics

---

### 6. Cloud Native Development (06-CloudNative/)
**Goal**: Design applications for cloud environments

- **Microservices**: Building and orchestrating microservices
- **HealthChecks**: Implementing readiness and liveness probes
- **Configuration**: External configuration, secrets management

**Key Skills**: 12-factor app principles, containerization, service discovery

---

### 7. Architecture Patterns (07-ArchitecturePatterns/)
**Goal**: Implement proven architectural patterns

- **CQRS-MediatR**: Full CQRS with MediatR and FluentValidation
- **Repository-UnitOfWork**: Data access patterns
- **Saga**: Distributed transaction management
- **Outbox**: Transactional outbox pattern for reliable messaging

**Key Skills**: Pattern selection, trade-offs, implementation strategies

---

### 8. Advanced Topics (08-AdvancedTopics/)
**Goal**: Master enterprise-grade architectural concepts

- **DomainDrivenDesign**: Strategic and tactical DDD patterns
  - Aggregates, value objects, domain events
  - Bounded contexts
- **EventSourcing**: Event-sourced systems
- **Resilience**: Polly, circuit breakers, retry patterns

**Key Skills**: Complex domain modeling, system resilience, failure handling

---

### 9. Enterprise CRUD API (09-EnterpriseCRUD/)
**Goal**: Build production-ready APIs combining all modern patterns

- **Task Management API**: Complete enterprise application
  - Clean Architecture with CQRS
  - OAuth2/OpenID Connect security
  - .NET Aspire orchestration
  - PostgreSQL with Entity Framework Core
  - Comprehensive validation and error handling

**Key Skills**: Integrating multiple patterns, production-ready development, cloud-native applications

---

## Getting Started

### Prerequisites
- .NET 8.0 SDK or later
- Visual Studio 2022 / Rider / VS Code
- Docker (for distributed systems examples)
- Basic understanding of C# and OOP principles

### Installation
```bash
# Clone the repository
git clone <your-repo-url>
cd netLearn

# Each project can be run independently
cd 01-DependencyInjection/BasicDI
dotnet restore
dotnet build
dotnet run
```

## Learning Approach

### Recommended Path
1. **Sequential Learning**: Follow modules 1-8 in order for a structured path
2. **Hands-On Practice**: Each project includes exercises and challenges
3. **Build Projects**: Apply concepts by building real-world scenarios
4. **Review & Refactor**: Revisit earlier modules with new knowledge

### For Each Module
1. Read the module README
2. Study the example code
3. Run and debug the projects
4. Complete the exercises
5. Build your own variation
6. Document your learnings

## Project Structure
```
netLearn/
├── 01-DependencyInjection/     # IoC and DI fundamentals
├── 02-AsynchronousProcessing/  # Async patterns and parallelism
├── 03-CleanArchitecture/       # Clean Architecture implementation
├── 04-VerticalSliceArchitecture/ # Vertical slice pattern
├── 05-DistributedSystems/      # Distributed architecture
├── 06-CloudNative/             # Cloud-native patterns
├── 07-ArchitecturePatterns/    # Common patterns
├── 08-AdvancedTopics/          # DDD, Event Sourcing, Resilience
└── 09-EnterpriseCRUD/          # Complete enterprise CRUD API
```

## Key Architectural Principles Covered

### SOLID Principles
- Single Responsibility Principle
- Open/Closed Principle
- Liskov Substitution Principle
- Interface Segregation Principle
- Dependency Inversion Principle

### Design Patterns
- Repository Pattern
- Unit of Work
- Factory Pattern
- Strategy Pattern
- Decorator Pattern
- CQRS
- Saga Pattern
- Outbox Pattern

### Architectural Styles
- Layered Architecture
- Clean Architecture
- Hexagonal Architecture (Ports & Adapters)
- Vertical Slice Architecture
- Event-Driven Architecture
- Microservices Architecture

## Best Practices

### Code Quality
- Write testable code (unit, integration, e2e tests)
- Follow SOLID principles
- Use meaningful naming conventions
- Keep methods small and focused
- Implement proper error handling

### Architecture
- Separate concerns (business logic vs infrastructure)
- Design for change and maintainability
- Consider scalability from the start
- Document architectural decisions (ADRs)
- Balance complexity vs simplicity

### Performance
- Async all the way
- Efficient data access patterns
- Caching strategies
- Connection pooling
- Resource management

## Resources

### Recommended Books
- "Clean Architecture" by Robert C. Martin
- "Domain-Driven Design" by Eric Evans
- "Patterns of Enterprise Application Architecture" by Martin Fowler
- "Building Microservices" by Sam Newman
- "Software Architecture: The Hard Parts" by Neal Ford et al.

### Online Resources
- Microsoft .NET Documentation
- Martin Fowler's Blog
- .NET Blog (devblogs.microsoft.com/dotnet)
- Architecture Weekly by Oskar Dudycz

### Courses & Videos
- Pluralsight: .NET Architecture Path
- YouTube: Milan Jovanović, Nick Chapsas
- Microsoft Learn: Cloud Architecture

## Progress Tracking

Use this checklist to track your progress:

- [ ] 01-DependencyInjection
  - [ ] BasicDI
  - [ ] DILifetimes
  - [ ] AdvancedDI
- [ ] 02-AsynchronousProcessing
  - [ ] AsyncAwait
  - [ ] TaskParallelLibrary
  - [ ] Channels
- [ ] 03-CleanArchitecture
  - [ ] CleanArchitectureDemo
- [ ] 04-VerticalSliceArchitecture
  - [ ] VerticalSliceDemo
- [ ] 05-DistributedSystems
  - [ ] MessageQueues
  - [ ] EventDriven
  - [ ] CQRS
- [ ] 06-CloudNative
  - [ ] Microservices
  - [ ] HealthChecks
  - [ ] Configuration
- [ ] 07-ArchitecturePatterns
  - [ ] CQRS-MediatR
  - [ ] Repository-UnitOfWork
  - [ ] Saga
  - [ ] Outbox
- [ ] 08-AdvancedTopics
  - [ ] DomainDrivenDesign
  - [ ] EventSourcing
  - [ ] Resilience
- [ ] 09-EnterpriseCRUD
  - [ ] Task Management API with Clean Architecture
  - [ ] CQRS implementation
  - [ ] OAuth2/OpenID Connect security
  - [ ] .NET Aspire orchestration

## Contributing to Your Learning

### Exercise Completion
Each module contains exercises. Document your solutions and learnings:
1. Create a `notes.md` in each project folder
2. Document problems you solved
3. Note alternative approaches you considered
4. Record performance observations

### Build Portfolio Projects
Apply these concepts to build real-world projects:
- E-commerce platform
- Task management system
- Blog/CMS system
- Notification service
- Analytics dashboard

## Next Steps

1. Start with [01-DependencyInjection/BasicDI](01-DependencyInjection/)
2. Set up your development environment
3. Work through each module systematically
4. Build a capstone project combining all concepts

## License

This repository is for personal learning purposes.

---

**Happy Learning!** Remember: becoming a great architect is a journey, not a destination. Focus on understanding the "why" behind patterns, not just the "how".
