# .NET Learning Path for Software Architects

A comprehensive, hands-on learning repository designed to master .NET architecture concepts and patterns. This repository contains practical projects and examples to help you become a successful software architect.

## Current Status

Every module builds, runs and is covered by tests. This table is the source of
truth for what is where:

| Module | Written material | Runnable code | Status |
|---|---|---|---|
| 01 · BasicDI | ✅ | ✅ | **Ready** |
| 01 · DILifetimes | ✅ | ✅ | **Ready** |
| 01 · AdvancedDI | ✅ | ✅ | **Ready** — 31 tests |
| 02 · AsyncAwait | ✅ | ✅ | **Ready** — 19 tests |
| 02 · TaskParallelLibrary | ✅ | ✅ | **Ready** — 18 tests |
| 02 · Channels | ✅ | ✅ | **Ready** — 18 tests |
| 03 · Clean Architecture | ✅ | ✅ | **Ready** — 41 tests |
| 04 · Vertical Slice | ✅ | ✅ | **Ready** — 23 tests |
| 05 · Distributed Systems | ✅ | ✅ | **Ready** — 17 tests |
| 06 · Cloud Native | ✅ | ✅ | **Ready** — 22 tests |
| 07 · Architecture Patterns | ✅ | ✅ | **Ready** — 24 tests |
| 08 · Advanced Topics | ✅ | ✅ | **Ready** — 32 tests |
| 09 · Enterprise CRUD | ✅ | ✅ | **Ready** — 62 tests |
| 10 · EF Core & PostgreSQL | ✅ | ✅ | **Ready** — 145 tests |
| 11 · NoSQL & Cosmos DB | ✅ | ✅ | **Ready** — 89 tests |
| 12 · Serverless Azure Functions | ✅ | ✅ | **Ready** — 10 tests |

**All twelve modules are complete.** `dotnet build netLearn.sln` builds 91
projects; `dotnet test netLearn.sln` runs **551 tests**.

Everything through module 09 runs with no external services (module 09
additionally offers PostgreSQL via .NET Aspire and Keycloak via Docker
Compose, both optional). Module 06 adds one Docker-and-Azure-only project,
`06-CloudNative/Aspire`, on top of that module's otherwise dependency-free
other three — its Part A (local Aspire orchestration) needs Docker, its
Part B (deploying to Azure with `azd`) needs a real Azure subscription and
is entirely opt-in. Modules 10 and 11 both need Docker: module 10 is
PostgreSQL-only (`docker compose up -d` from `10-EntityFrameworkCore/` for
the reference projects; its tests spin up their own throwaway Postgres via
Testcontainers), and module 11 is Cosmos DB-only, orchestrated with .NET
Aspire instead — each project's own `AppHost` starts the Cosmos DB emulator
(a heavier container than Postgres, with a multi-minute cold start), and its
Aspire-based integration tests do the same. Module 12 needs no Docker at
all: its C# work (an Entra ID-secured Azure Function, no database) builds
and its 10 tests run and pass with nothing but the .NET SDK — only its
Bicep/deployment section (Part 4) touches Azure, and that's the user's own
subscription to run, same opt-in policy as modules 06/10/11.

## Learning Path Overview

This repository is organized into 12 progressive modules, each focusing on critical architectural concepts:

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
- **Aspire**: The full .NET Aspire feature tour (AppHost, ServiceDefaults,
  service discovery, container resources, client integrations, the
  dashboard, testing) and deploying the same app to Azure with `azd`

**Key Skills**: 12-factor app principles, containerization, service discovery, Aspire orchestration, Azure deployment

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

### 10. Entity Framework Core & PostgreSQL (10-EntityFrameworkCore/)
**Goal**: Master EF Core against real PostgreSQL — modeling, migrations, querying, transactions, and operational concerns

- **EfCoreModeling**: Fluent API, relationships, owned types, value converters, many-to-many with a payload, inheritance, constraints
- **EfCoreMigrations**: Creating, hand-editing, seeding, applying, and rolling back migrations
- **EfCoreQuerying**: LINQ filtering/projection/grouping, `Include` vs. projections, raw SQL, and Postgres-only querying (`ILIKE`, arrays, JSONB)
- **EfCoreTransactions**: Explicit transactions, savepoints, optimistic concurrency via `xmin`, isolation levels
- **EfCoreLoggingAndHealthChecks**: EF Core logging, interceptors, and readiness/liveness health checks

**Key Skills**: Schema modeling and evolution, query performance, transactional correctness, production diagnostics — all PostgreSQL-specific, not simulated on SQLite

---

### 11. NoSQL with Azure Cosmos DB and .NET Aspire (11-NoSqlCosmosDb/)
**Goal**: Master document modeling and operations on Cosmos DB, orchestrated with .NET Aspire, and understand where it deliberately diverges from module 10's relational approach

- **CosmosModeling**: Partition key selection, embedding vs. referencing, schema evolution without migrations, synthetic partition keys
- **CosmosQuerying**: The LINQ provider and the parameterized SQL API, cross-partition vs. single-partition queries, pagination, RU-aware projections
- **CosmosIndexingAndThroughput**: Indexing policies, `RequestCharge`, manual vs. autoscale throughput, handling `429` responses
- **CosmosChangeFeed**: The change feed processor, a materialized read model, at-least-once delivery and idempotency
- **CosmosConsistencyAndTransactions**: The five consistency levels, ETag optimistic concurrency, `TransactionalBatch` within a single partition key

**Key Skills**: Partition-key-driven data modeling, RU cost management, event-driven read models, and the specific ways NoSQL trades relational guarantees (foreign keys, cross-table transactions, query-plan optimization) for horizontal scale — each project pairs directly with a module 10 counterpart to make the contrast concrete

---

### 12. Serverless Azure Functions (12-AzureFunctionsServerless/)
**Goal**: Secure a serverless API with real Entra ID authentication and automate its infrastructure with Bicep — no database, so the request pipeline itself stays the focus

- **Part 1 — Build the Function API**: isolated-worker fundamentals, why a Storage Account is required even with no application data, `AuthorizationLevel.Anonymous` vs. real authentication
- **Part 2 — Secure it with Entra ID**: a hand-rolled `IFunctionsWorkerMiddleware` that validates bearer tokens (issuer, audience, signature, lifetime, app role) directly, two App Registrations, and the client-credentials flow
- **Part 3 — Managed Identity + Key Vault**: reading a secret with no key or connection string anywhere in configuration, and RBAC as the permission half of that story
- **Part 4 — Infrastructure as Code with Bicep**: `main.bicep` and its modules, why an Entra ID App Registration needs a `deploymentScript` instead of a native Bicep resource, and deploying/tearing down for real

**Key Skills**: Serverless request handling, hand-validating OAuth2/OIDC bearer tokens instead of trusting a framework to hide it, Managed Identity vs. RBAC, and Bicep as more than a YAML-for-ARM exercise — most of this module's logic (the token validator, the notes store) is unit-tested with zero Azure dependency, a first for the repo's Azure-touching modules

---

## Getting Started

### Prerequisites
- .NET 9.0 SDK (pinned in `global.json`; every project targets `net9.0`)
- Visual Studio 2022 / Rider / VS Code
- Docker (for distributed systems examples, and required for modules 10-11)
- Basic understanding of C# and OOP principles

### Installation
```bash
# Clone the repository
git clone <your-repo-url>
cd netLearn

# Build and test everything
dotnet build netLearn.sln
dotnet test netLearn.sln          # 551 tests (modules 10-11 need Docker; module 12's need nothing)

# Or start with the first module
dotnet run --project 01-DependencyInjection/BasicDI/BasicDI
```

## Learning Approach

### Recommended Path
1. **Sequential Learning**: Follow modules 1-9 in order for a structured path
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
├── netLearn.sln                # All projects; `dotnet build` at the root builds everything
├── global.json                 # Pins the .NET SDK
├── Directory.Build.props       # Shared TargetFramework / nullable / warning settings
├── Directory.Packages.props    # Central package versions (csproj files omit Version)
├── 01-DependencyInjection/     # IoC and DI fundamentals
├── 02-AsynchronousProcessing/  # Async patterns and parallelism
├── 03-CleanArchitecture/       # Clean Architecture implementation
├── 04-VerticalSliceArchitecture/ # Vertical slice pattern
├── 05-DistributedSystems/      # Distributed architecture
├── 06-CloudNative/             # Cloud-native patterns
├── 07-ArchitecturePatterns/    # Common patterns
├── 08-AdvancedTopics/          # DDD, Event Sourcing, Resilience
├── 09-EnterpriseCRUD/          # Complete enterprise CRUD API
├── 10-EntityFrameworkCore/     # EF Core against PostgreSQL
├── 11-NoSqlCosmosDb/           # Cosmos DB with .NET Aspire
└── 12-AzureFunctionsServerless/ # Serverless Functions, Entra ID, Bicep
```

### Anatomy of a Project

Every exercise project follows the same shape, so once you've done one you know
your way around all of them:

```
<Module>/<Project>/
├── README.md           # The concepts: what this teaches and why it matters
├── GETTING_STARTED.md  # How to run it, and how the folders are laid out
├── EXERCISE.md         # Step-by-step work for you to do
├── <Project>/          # Your workspace — you write the code here
├── solution/           # Reference implementation, for when you get stuck
└── tests/              # Tests proving the behaviour the exercise teaches
```

Try the exercise first and only open `solution/` to compare afterwards — reading
it early is the fastest way to feel productive and learn nothing.

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
  - [ ] Aspire
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
- [ ] 10-EntityFrameworkCore
  - [ ] EfCoreModeling
  - [ ] EfCoreMigrations
  - [ ] EfCoreQuerying
  - [ ] EfCoreTransactions
  - [ ] EfCoreLoggingAndHealthChecks
- [ ] 11-NoSqlCosmosDb
  - [ ] CosmosModeling
  - [ ] CosmosQuerying
  - [ ] CosmosIndexingAndThroughput
  - [ ] CosmosChangeFeed
  - [ ] CosmosConsistencyAndTransactions
- [ ] 12-AzureFunctionsServerless
  - [ ] Part 1 - Build the Function API
  - [ ] Part 2 - Secure it with Entra ID
  - [ ] Part 3 - Managed Identity + Key Vault
  - [ ] Part 4 - Infrastructure as Code with Bicep

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
