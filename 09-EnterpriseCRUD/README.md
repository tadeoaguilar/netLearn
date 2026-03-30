# Module 9: Enterprise CRUD API

## Overview
Build a production-ready, enterprise-grade CRUD API using modern .NET patterns and practices. This comprehensive module combines Clean Architecture, CQRS, OAuth2/OpenID Connect security, .NET Aspire for orchestration, and PostgreSQL database - all integrated into a complete Task Management system.

## Learning Objectives
- Implement Clean Architecture for maintainable enterprise applications
- Apply CQRS pattern to separate read and write operations
- Secure APIs with OAuth2/OpenID Connect authentication
- Use .NET Aspire for cloud-native application orchestration
- Integrate PostgreSQL with Entity Framework Core
- Master Dependency Injection in complex scenarios
- Build production-ready RESTful APIs
- Implement validation, error handling, and logging
- Apply domain-driven design principles

## What You'll Build

A **Task Management API** with the following features:

### Core Entities
- **Projects**: Containers for organizing tasks
- **Tasks**: Individual work items with status, priority, and assignments

### Features
- Full CRUD operations for Projects and Tasks
- Advanced querying and filtering
- Task assignment and status tracking
- OAuth2/OpenID Connect authentication
- Authorization with role-based access control
- Audit logging
- Health checks
- Distributed tracing
- API versioning

## Technology Stack

### Core Framework
- **.NET 8.0**: Latest LTS version
- **ASP.NET Core Web API**: RESTful API framework

### Architecture Patterns
- **Clean Architecture**: Separation of concerns with dependency inversion
- **CQRS**: Command Query Responsibility Segregation
- **Repository Pattern**: Data access abstraction
- **Unit of Work**: Transaction management

### Libraries & Tools
- **MediatR**: CQRS and mediator pattern implementation
- **FluentValidation**: Validation rules
- **Entity Framework Core**: ORM for PostgreSQL
- **AutoMapper**: Object-to-object mapping
- **Serilog**: Structured logging
- **Swashbuckle**: OpenAPI/Swagger documentation

### Security
- **OAuth2/OpenID Connect**: Industry-standard authentication
- **IdentityServer** or **Keycloak**: Identity provider options
- **JWT Bearer**: Token-based authentication

### Infrastructure
- **.NET Aspire**: Orchestration and observability
- **PostgreSQL**: Production database
- **Docker**: Containerization
- **Redis**: Caching (optional)

## Project Structure

```
09-EnterpriseCRUD/
├── README.md                          # This file
├── GETTING_STARTED.md                 # Step-by-step guide with all code
├── TaskManagement.sln                 # Solution file
│
├── src/
│   ├── TaskManagement.Domain/         # Enterprise business rules
│   │   ├── Entities/
│   │   │   ├── Project.cs
│   │   │   ├── Task.cs
│   │   │   └── Common/
│   │   ├── ValueObjects/
│   │   ├── Enums/
│   │   ├── Events/
│   │   └── Exceptions/
│   │
│   ├── TaskManagement.Application/    # Application business rules
│   │   ├── Common/
│   │   │   ├── Interfaces/
│   │   │   ├── Behaviours/
│   │   │   └── Mappings/
│   │   ├── Projects/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateProject/
│   │   │   │   ├── UpdateProject/
│   │   │   │   └── DeleteProject/
│   │   │   └── Queries/
│   │   │       ├── GetProject/
│   │   │       └── GetProjects/
│   │   └── Tasks/
│   │       ├── Commands/
│   │       └── Queries/
│   │
│   ├── TaskManagement.Infrastructure/ # External concerns
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/
│   │   │   ├── Repositories/
│   │   │   └── Migrations/
│   │   ├── Identity/
│   │   │   └── IdentityService.cs
│   │   └── Services/
│   │       ├── DateTimeService.cs
│   │       └── EmailService.cs
│   │
│   ├── TaskManagement.WebApi/         # API presentation layer
│   │   ├── Controllers/
│   │   │   ├── ProjectsController.cs
│   │   │   └── TasksController.cs
│   │   ├── Filters/
│   │   ├── Middleware/
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   └── TaskManagement.AppHost/        # .NET Aspire orchestration
│       └── Program.cs
│
└── tests/
    ├── TaskManagement.Application.Tests/
    ├── TaskManagement.Domain.Tests/
    └── TaskManagement.Integration.Tests/
```

## Architecture Layers Explained

### 1. Domain Layer (Core)
**Zero Dependencies - Pure Business Logic**

The heart of your application containing:
- **Entities**: `Project`, `Task` with business rules
- **Value Objects**: Immutable types like `Priority`, `TaskStatus`
- **Domain Events**: Events raised by entity actions
- **Domain Exceptions**: Business rule violations

**Key Principle**: No dependencies on infrastructure, frameworks, or UI

```csharp
// Example: Task entity with business rules
public class Task : BaseEntity
{
    public string Title { get; private set; }
    public TaskStatus Status { get; private set; }

    public void Complete()
    {
        if (Status == TaskStatus.Completed)
            throw new InvalidOperationException("Task already completed");

        Status = TaskStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        // Raise domain event
        AddDomainEvent(new TaskCompletedEvent(this));
    }
}
```

### 2. Application Layer
**Business Logic Orchestration**

Implements use cases using:
- **Commands**: Write operations (Create, Update, Delete)
- **Queries**: Read operations (Get, List, Search)
- **Handlers**: Process commands and queries
- **Validators**: FluentValidation rules
- **Interfaces**: Abstractions for infrastructure

**Pattern**: CQRS with MediatR

```csharp
// Command
public record CreateProjectCommand : IRequest<Guid>
{
    public string Name { get; init; }
    public string Description { get; init; }
}

// Handler
public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public async Task<Guid> Handle(CreateProjectCommand request, CancellationToken ct)
    {
        var project = new Project(request.Name, request.Description);
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(ct);
        return project.Id;
    }
}
```

### 3. Infrastructure Layer
**External Concerns Implementation**

Implements interfaces defined in Application layer:
- **Database**: EF Core with PostgreSQL
- **Identity**: OAuth2/OpenID Connect
- **External Services**: Email, SMS, etc.
- **File Storage**: Local or cloud storage

**Dependency Direction**: Depends on Application and Domain

### 4. Web API Layer
**HTTP Interface**

RESTful API exposing functionality:
- **Controllers**: HTTP endpoints
- **DTOs**: Request/Response models
- **Middleware**: Authentication, error handling, logging
- **Filters**: Cross-cutting concerns

### 5. AppHost Layer (.NET Aspire)
**Orchestration & Observability**

Manages the entire application stack:
- Service discovery
- Configuration management
- Health checks
- Distributed tracing
- Container orchestration

## Key Patterns & Practices

### CQRS (Command Query Responsibility Segregation)
Separates read and write operations for optimal performance and scalability.

**Benefits**:
- Different models for reads vs writes
- Optimized queries without affecting commands
- Scalability (scale reads independently)
- Clearer code organization

**Implementation**: MediatR with Commands and Queries

### Clean Architecture Benefits
1. **Independent of Frameworks**: Can change EF Core to Dapper without touching business logic
2. **Testable**: Business rules testable without database, UI, or external services
3. **Independent of UI**: Can add Blazor, React, or Mobile UI without changing business logic
4. **Independent of Database**: Can swap PostgreSQL for MongoDB
5. **Independent of External Services**: Business rules don't depend on external APIs

### OAuth2/OpenID Connect Security

**Why OAuth2/OIDC?**
- Industry standard
- Centralized authentication
- Single Sign-On (SSO) support
- Token-based (stateless)
- Suitable for microservices

**Flow**:
1. User authenticates with Identity Provider
2. Identity Provider issues JWT token
3. Client includes token in API requests
4. API validates token and authorizes access

### .NET Aspire Benefits

**Orchestration**:
- Simplified multi-project startup
- Service-to-service communication
- Automatic configuration

**Observability**:
- Built-in telemetry
- Distributed tracing
- Health checks dashboard
- Logs aggregation

## Security Implementation

### Authentication Flow
```
User → Identity Provider (Keycloak/IdentityServer)
           ↓
       JWT Token
           ↓
    API (validates token)
           ↓
   Authorized Access
```

### Authorization Levels
- **Anonymous**: Public endpoints
- **Authenticated**: Must have valid token
- **Role-based**: Admin, Manager, User roles
- **Resource-based**: Can only edit own tasks

### Security Features
- JWT token validation
- Role-based authorization
- Claim-based policies
- HTTPS enforcement
- CORS configuration
- Rate limiting
- API key support (optional)

## Database Design

### Schema
```sql
Projects
├── Id (uuid, PK)
├── Name (varchar)
├── Description (text)
├── OwnerId (varchar)
├── CreatedAt (timestamp)
└── UpdatedAt (timestamp)

Tasks
├── Id (uuid, PK)
├── ProjectId (uuid, FK)
├── Title (varchar)
├── Description (text)
├── Status (enum)
├── Priority (enum)
├── AssignedToId (varchar)
├── DueDate (timestamp)
├── CreatedAt (timestamp)
├── UpdatedAt (timestamp)
└── CompletedAt (timestamp)
```

### Relationships
- One Project has many Tasks
- One User owns many Projects
- One User can be assigned many Tasks

## API Endpoints

### Projects
```
GET    /api/v1/projects           - List all projects
GET    /api/v1/projects/{id}      - Get project by ID
POST   /api/v1/projects           - Create project
PUT    /api/v1/projects/{id}      - Update project
DELETE /api/v1/projects/{id}      - Delete project
GET    /api/v1/projects/{id}/tasks - Get project tasks
```

### Tasks
```
GET    /api/v1/tasks              - List all tasks
GET    /api/v1/tasks/{id}         - Get task by ID
POST   /api/v1/tasks              - Create task
PUT    /api/v1/tasks/{id}         - Update task
DELETE /api/v1/tasks/{id}         - Delete task
PATCH  /api/v1/tasks/{id}/status  - Update task status
PATCH  /api/v1/tasks/{id}/assign  - Assign task
```

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Docker Desktop
- PostgreSQL (via Docker)
- Visual Studio 2022 / Rider / VS Code
- Postman or similar API testing tool

### Quick Start
Follow the comprehensive [GETTING_STARTED.md](GETTING_STARTED.md) guide which includes:
1. Environment setup
2. .NET Aspire installation
3. PostgreSQL setup with Docker
4. Identity Provider setup (Keycloak)
5. Complete code for all layers
6. Step-by-step build instructions
7. Testing and validation

## Learning Path

### Phase 1: Foundation (2-3 hours)
1. Read this README completely
2. Understand Clean Architecture concepts
3. Review CQRS pattern
4. Understand OAuth2/OIDC basics

### Phase 2: Setup (1-2 hours)
1. Install prerequisites
2. Set up PostgreSQL
3. Set up Keycloak (Identity Provider)
4. Configure .NET Aspire

### Phase 3: Implementation (4-6 hours)
1. Create solution structure
2. Implement Domain layer
3. Implement Application layer with CQRS
4. Implement Infrastructure layer
5. Implement Web API layer
6. Configure Aspire orchestration

### Phase 4: Testing (1-2 hours)
1. Test API endpoints
2. Test authentication flow
3. Test authorization rules
4. Explore Aspire dashboard

### Phase 5: Extensions (Optional)
1. Add caching with Redis
2. Implement event sourcing
3. Add real-time updates with SignalR
4. Implement background jobs
5. Add file attachments to tasks

## Best Practices Demonstrated

### Code Organization
- Feature folders in Application layer
- One command/query per file
- Consistent naming conventions
- Clear separation of concerns

### Error Handling
- Custom exceptions for domain rules
- Global exception middleware
- Structured error responses
- Proper HTTP status codes

### Validation
- FluentValidation for commands
- Domain validation in entities
- API model validation
- Business rule validation

### Testing Strategy
- Unit tests for domain logic
- Unit tests for handlers
- Integration tests for API
- Test authentication/authorization

### Performance
- Async/await throughout
- EF Core query optimization
- Projection for read models
- Pagination for lists
- Caching strategies

### Observability
- Structured logging with Serilog
- Distributed tracing
- Health checks
- Metrics collection

## Common Challenges & Solutions

### Challenge 1: Circular Dependencies
**Problem**: Infrastructure needs Application interfaces, Application needs Domain
**Solution**: Dependency Inversion - Interfaces in Application, implementations in Infrastructure

### Challenge 2: Entity vs DTO Mapping
**Problem**: Exposing domain entities directly in API
**Solution**: Use AutoMapper for DTO mapping, never expose entities

### Challenge 3: Transaction Management
**Problem**: Ensuring consistency across multiple operations
**Solution**: Unit of Work pattern with SaveChangesAsync

### Challenge 4: Authentication Testing
**Problem**: Testing endpoints with OAuth2
**Solution**: Token generator for testing, mock authentication in tests

### Challenge 5: Aspire Complexity
**Problem**: Understanding Aspire project structure
**Solution**: Start simple, add components incrementally

## Comparison with Other Approaches

| Aspect | This Module | Simple CRUD | Layered Architecture |
|--------|-------------|-------------|----------------------|
| Complexity | High | Low | Medium |
| Maintainability | Excellent | Poor | Good |
| Testability | Excellent | Fair | Good |
| Scalability | Excellent | Limited | Good |
| Learning Curve | Steep | Easy | Moderate |
| Best For | Enterprise | Prototypes | Standard Apps |

## Related Modules

### Prerequisites
- [01-DependencyInjection](../01-DependencyInjection/) - Essential
- [03-CleanArchitecture](../03-CleanArchitecture/) - Essential
- [07-ArchitecturePatterns](../07-ArchitecturePatterns/) - CQRS knowledge

### Builds Upon
- Clean Architecture concepts from Module 3
- CQRS pattern from Module 7
- DI concepts from Module 1

### Extends To
- [05-DistributedSystems](../05-DistributedSystems/) - Event-driven patterns
- [08-AdvancedTopics](../08-AdvancedTopics/) - DDD and Event Sourcing

## Additional Resources

### Documentation
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Clean Architecture by Uncle Bob](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [OAuth 2.0 and OpenID Connect](https://oauth.net/2/)
- [MediatR Documentation](https://github.com/jbogard/MediatR)

### Recommended Reading
- "Clean Architecture" by Robert C. Martin
- "Implementing Domain-Driven Design" by Vaughn Vernon
- "Building Microservices" by Sam Newman

### Video Resources
- Milan Jovanović - Clean Architecture series
- Nick Chapsas - .NET Performance and Patterns
- Microsoft Learn - .NET Aspire tutorials

## Exercises

### Exercise 1: Basic CRUD
Complete the basic Task Management API following GETTING_STARTED.md

### Exercise 2: Add Comments Feature
- Add Comments entity
- Implement CRUD operations
- Associate comments with tasks
- Add user mentions

### Exercise 3: Advanced Queries
- Search tasks by criteria
- Filter by status, priority, assignee
- Sort by multiple fields
- Implement pagination

### Exercise 4: Notifications
- Send email when task assigned
- Notify on task status changes
- Daily digest of pending tasks

### Exercise 5: Audit Trail
- Track all entity changes
- Store who changed what and when
- Provide audit log API

### Exercise 6: Performance Optimization
- Add Redis caching
- Implement query result caching
- Optimize N+1 query problems
- Add database indexes

## Troubleshooting

### Common Issues

**Issue**: Aspire won't start
- Ensure Docker is running
- Check port availability (5000-5005)
- Review Aspire installation

**Issue**: PostgreSQL connection fails
- Verify Docker container is running
- Check connection string
- Ensure database is created

**Issue**: Authentication fails
- Verify Keycloak is running
- Check token validity
- Review JWT configuration

**Issue**: Migrations fail
- Delete Migrations folder
- Run `dotnet ef migrations add Initial`
- Check database permissions

## Success Criteria

By the end of this module, you should be able to:
- [ ] Explain Clean Architecture layers and dependencies
- [ ] Implement CQRS with MediatR
- [ ] Configure OAuth2/OpenID Connect authentication
- [ ] Use .NET Aspire for orchestration
- [ ] Design and implement PostgreSQL schema
- [ ] Create RESTful API endpoints
- [ ] Write unit and integration tests
- [ ] Apply dependency injection patterns
- [ ] Implement validation and error handling
- [ ] Use structured logging and tracing

## Next Steps

After completing this module:
1. Build a real-world project using these patterns
2. Explore Event Sourcing in Module 8
3. Add event-driven communication (Module 5)
4. Implement microservices architecture
5. Deploy to cloud (Azure/AWS)

## Feedback & Contributions

This is a learning repository. Feel free to:
- Experiment with different approaches
- Add your own features
- Document your learnings
- Share your implementations

---

**Ready to build enterprise-grade APIs?** Open [GETTING_STARTED.md](GETTING_STARTED.md) and begin your journey!
