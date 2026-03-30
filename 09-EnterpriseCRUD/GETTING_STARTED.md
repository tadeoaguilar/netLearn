# Getting Started with Enterprise CRUD API

This comprehensive guide will walk you through building a production-ready Task Management API using Clean Architecture, CQRS, OAuth2/OpenID Connect, .NET Aspire, and PostgreSQL.

**Estimated Time**: 6-8 hours
**Difficulty**: Advanced

---

## Table of Contents
1. [Environment Setup](#step-1-environment-setup)
2. [Create Solution Structure](#step-2-create-solution-structure)
3. [Domain Layer Implementation](#step-3-domain-layer-implementation)
4. [Application Layer with CQRS](#step-4-application-layer-with-cqrs)
5. [Infrastructure Layer](#step-5-infrastructure-layer)
6. [Web API Layer](#step-6-web-api-layer)
7. [.NET Aspire Integration](#step-7-net-aspire-integration)
8. [PostgreSQL Setup](#step-8-postgresql-setup)
9. [OAuth2/OpenID Connect Security](#step-9-oauth2openid-connect-security)
10. [Testing the API](#step-10-testing-the-api)

---

## Step 1: Environment Setup

### 1.1 Prerequisites

Ensure you have the following installed:

```bash
# Verify .NET 8.0 SDK
dotnet --version
# Should output: 8.0.x or higher

# Verify Docker
docker --version
# Should output: Docker version 20.x or higher

# Check Docker is running
docker ps
```

### 1.2 Install .NET Aspire Workload

.NET Aspire is a cloud-ready stack for building observable, production-ready applications.

```bash
# Install Aspire workload
dotnet workload update
dotnet workload install aspire

# Verify installation
dotnet workload list
# Should show: aspire
```

**What is .NET Aspire?**
- **Orchestration**: Manages multi-project applications
- **Service Discovery**: Automatic service-to-service communication
- **Observability**: Built-in telemetry, logging, and health checks
- **Components**: Pre-configured integrations (PostgreSQL, Redis, etc.)

### 1.3 Install Required Tools

```bash
# Install EF Core tools globally
dotnet tool install --global dotnet-ef

# Verify installation
dotnet ef --version
```

### 1.4 IDE Setup

Choose your preferred IDE:
- **Visual Studio 2022** (v17.9+): Full Aspire support with dashboard
- **JetBrains Rider** (2024.1+): Excellent C# support
- **VS Code**: Use C# Dev Kit extension

---

## Step 2: Create Solution Structure

### 2.1 Navigate to Module Folder

```bash
cd /Users/tadeo/gitlab/netLearn/09-EnterpriseCRUD
```

### 2.2 Create Aspire Application

```bash
# Create Aspire starter app (includes AppHost and ServiceDefaults)
dotnet new aspire-starter -n TaskManagement

# Navigate into the solution
cd TaskManagement
```

**What This Creates**:
- `TaskManagement.AppHost/` - Orchestration project
- `TaskManagement.ServiceDefaults/` - Shared configuration
- `TaskManagement.Web/` - Sample web project (we'll replace this)

### 2.3 Remove Sample Project

```bash
# Remove the sample web project
rm -rf TaskManagement.Web

# Or on Windows
# rmdir /s TaskManagement.Web
```

### 2.4 Create Our Project Structure

```bash
# Create src folder for application code
mkdir src
cd src

# Create Domain layer (no dependencies)
dotnet new classlib -n TaskManagement.Domain
rm TaskManagement.Domain/Class1.cs

# Create Application layer
dotnet new classlib -n TaskManagement.Application
rm TaskManagement.Application/Class1.cs

# Create Infrastructure layer
dotnet new classlib -n TaskManagement.Infrastructure
rm TaskManagement.Infrastructure/Class1.cs

# Create Web API project
dotnet new webapi -n TaskManagement.WebApi

# Go back to solution root
cd ..
```

### 2.5 Update Solution File

```bash
# Remove old web project reference
dotnet sln remove TaskManagement.Web

# Add our projects to solution
dotnet sln add src/TaskManagement.Domain/TaskManagement.Domain.csproj
dotnet sln add src/TaskManagement.Application/TaskManagement.Application.csproj
dotnet sln add src/TaskManagement.Infrastructure/TaskManagement.Infrastructure.csproj
dotnet sln add src/TaskManagement.WebApi/TaskManagement.WebApi.csproj
```

### 2.6 Set Up Project References

```bash
# Application depends on Domain
cd src/TaskManagement.Application
dotnet add reference ../TaskManagement.Domain/TaskManagement.Domain.csproj

# Infrastructure depends on Application (and transitively Domain)
cd ../TaskManagement.Infrastructure
dotnet add reference ../TaskManagement.Application/TaskManagement.Application.csproj

# WebApi depends on Application and Infrastructure
cd ../TaskManagement.WebApi
dotnet add reference ../TaskManagement.Application/TaskManagement.Application.csproj
dotnet add reference ../TaskManagement.Infrastructure/TaskManagement.Infrastructure.csproj

# Go back to solution root
cd ../..
```

### 2.7 Verify Solution Structure

```bash
# Build solution to verify everything is set up correctly
dotnet build

# You should see:
# Build succeeded.
```

---

## Step 3: Domain Layer Implementation

The Domain layer contains pure business logic with ZERO dependencies on external frameworks.

### 3.1 Create Common Base Classes

**File**: `src/TaskManagement.Domain/Common/BaseEntity.cs`

```csharp
namespace TaskManagement.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    protected void MarkAsUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
```

**File**: `src/TaskManagement.Domain/Common/IDomainEvent.cs`

```csharp
namespace TaskManagement.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
```

**File**: `src/TaskManagement.Domain/Common/BaseAuditableEntity.cs`

```csharp
namespace TaskManagement.Domain.Common;

public abstract class BaseAuditableEntity : BaseEntity
{
    public string CreatedBy { get; protected set; } = string.Empty;
    public string? LastModifiedBy { get; protected set; }
}
```

### 3.2 Create Enums

**File**: `src/TaskManagement.Domain/Enums/TaskStatus.cs`

```csharp
namespace TaskManagement.Domain.Enums;

public enum TaskStatus
{
    NotStarted = 0,
    InProgress = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4
}
```

**File**: `src/TaskManagement.Domain/Enums/TaskPriority.cs`

```csharp
namespace TaskManagement.Domain.Enums;

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
```

### 3.3 Create Value Objects

**File**: `src/TaskManagement.Domain/ValueObjects/Priority.cs`

```csharp
namespace TaskManagement.Domain.ValueObjects;

public record Priority
{
    public TaskPriority Value { get; }

    private Priority(TaskPriority value)
    {
        Value = value;
    }

    public static Priority Low => new(TaskPriority.Low);
    public static Priority Medium => new(TaskPriority.Medium);
    public static Priority High => new(TaskPriority.High);
    public static Priority Critical => new(TaskPriority.Critical);

    public static Priority FromValue(TaskPriority value) => new(value);

    public bool IsHigherThan(Priority other) => Value > other.Value;
    public bool IsLowerThan(Priority other) => Value < other.Value;
}
```

### 3.4 Create Domain Exceptions

**File**: `src/TaskManagement.Domain/Exceptions/DomainException.cs`

```csharp
namespace TaskManagement.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

**File**: `src/TaskManagement.Domain/Exceptions/InvalidTaskStatusException.cs`

```csharp
namespace TaskManagement.Domain.Exceptions;

public class InvalidTaskStatusException : DomainException
{
    public InvalidTaskStatusException(string message) : base(message)
    {
    }
}
```

**File**: `src/TaskManagement.Domain/Exceptions/InvalidProjectException.cs`

```csharp
namespace TaskManagement.Domain.Exceptions;

public class InvalidProjectException : DomainException
{
    public InvalidProjectException(string message) : base(message)
    {
    }
}
```

### 3.5 Create Domain Events

**File**: `src/TaskManagement.Domain/Events/TaskCompletedEvent.cs`

```csharp
using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Events;

public record TaskCompletedEvent : IDomainEvent
{
    public Guid TaskId { get; init; }
    public string TaskTitle { get; init; }
    public DateTime OccurredOn { get; init; }

    public TaskCompletedEvent(Guid taskId, string taskTitle)
    {
        TaskId = taskId;
        TaskTitle = taskTitle;
        OccurredOn = DateTime.UtcNow;
    }
}
```

**File**: `src/TaskManagement.Domain/Events/TaskAssignedEvent.cs`

```csharp
using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Events;

public record TaskAssignedEvent : IDomainEvent
{
    public Guid TaskId { get; init; }
    public string AssignedToId { get; init; }
    public DateTime OccurredOn { get; init; }

    public TaskAssignedEvent(Guid taskId, string assignedToId)
    {
        TaskId = taskId;
        AssignedToId = assignedToId;
        OccurredOn = DateTime.UtcNow;
    }
}
```

**File**: `src/TaskManagement.Domain/Events/ProjectCreatedEvent.cs`

```csharp
using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Events;

public record ProjectCreatedEvent : IDomainEvent
{
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; }
    public DateTime OccurredOn { get; init; }

    public ProjectCreatedEvent(Guid projectId, string projectName)
    {
        ProjectId = projectId;
        ProjectName = projectName;
        OccurredOn = DateTime.UtcNow;
    }
}
```

### 3.6 Create Entities

**File**: `src/TaskManagement.Domain/Entities/Project.cs`

```csharp
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Events;
using TaskManagement.Domain.Exceptions;

namespace TaskManagement.Domain.Entities;

public class Project : BaseAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string OwnerId { get; private set; } = string.Empty;
    public bool IsArchived { get; private set; }

    private readonly List<ProjectTask> _tasks = new();
    public IReadOnlyCollection<ProjectTask> Tasks => _tasks.AsReadOnly();

    // EF Core constructor
    private Project() { }

    public Project(string name, string description, string ownerId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidProjectException("Project name cannot be empty");

        if (string.IsNullOrWhiteSpace(ownerId))
            throw new InvalidProjectException("Project must have an owner");

        Name = name;
        Description = description;
        OwnerId = ownerId;
        CreatedBy = ownerId;
        IsArchived = false;

        AddDomainEvent(new ProjectCreatedEvent(Id, Name));
    }

    public void UpdateDetails(string name, string description, string userId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidProjectException("Project name cannot be empty");

        Name = name;
        Description = description;
        LastModifiedBy = userId;
        MarkAsUpdated();
    }

    public void Archive(string userId)
    {
        if (IsArchived)
            throw new InvalidProjectException("Project is already archived");

        IsArchived = true;
        LastModifiedBy = userId;
        MarkAsUpdated();
    }

    public void Restore(string userId)
    {
        if (!IsArchived)
            throw new InvalidProjectException("Project is not archived");

        IsArchived = false;
        LastModifiedBy = userId;
        MarkAsUpdated();
    }

    public void AddTask(ProjectTask task)
    {
        _tasks.Add(task);
        MarkAsUpdated();
    }

    public int GetActiveTaskCount() => _tasks.Count(t => t.Status != Enums.TaskStatus.Completed);
}
```

**File**: `src/TaskManagement.Domain/Entities/ProjectTask.cs`

```csharp
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Events;
using TaskManagement.Domain.Exceptions;

namespace TaskManagement.Domain.Entities;

public class ProjectTask : BaseAuditableEntity
{
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TaskStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public Guid ProjectId { get; private set; }
    public string? AssignedToId { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // Navigation property
    public Project Project { get; private set; } = null!;

    // EF Core constructor
    private ProjectTask() { }

    public ProjectTask(
        string title,
        string description,
        Guid projectId,
        string createdBy,
        TaskPriority priority = TaskPriority.Medium,
        DateTime? dueDate = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidTaskStatusException("Task title cannot be empty");

        Title = title;
        Description = description;
        ProjectId = projectId;
        Status = TaskStatus.NotStarted;
        Priority = priority;
        DueDate = dueDate;
        CreatedBy = createdBy;
    }

    public void UpdateDetails(string title, string description, string userId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidTaskStatusException("Task title cannot be empty");

        Title = title;
        Description = description;
        LastModifiedBy = userId;
        MarkAsUpdated();
    }

    public void UpdateStatus(TaskStatus newStatus, string userId)
    {
        if (Status == newStatus)
            return;

        // Validate status transitions
        if (Status == TaskStatus.Completed && newStatus != TaskStatus.NotStarted)
            throw new InvalidTaskStatusException("Completed tasks can only be reopened");

        if (Status == TaskStatus.Cancelled)
            throw new InvalidTaskStatusException("Cannot change status of cancelled task");

        Status = newStatus;
        LastModifiedBy = userId;
        MarkAsUpdated();

        if (newStatus == TaskStatus.Completed)
        {
            CompletedAt = DateTime.UtcNow;
            AddDomainEvent(new TaskCompletedEvent(Id, Title));
        }
    }

    public void SetPriority(TaskPriority priority, string userId)
    {
        Priority = priority;
        LastModifiedBy = userId;
        MarkAsUpdated();
    }

    public void AssignTo(string userId, string assignedBy)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidTaskStatusException("User ID cannot be empty");

        AssignedToId = userId;
        LastModifiedBy = assignedBy;
        MarkAsUpdated();

        AddDomainEvent(new TaskAssignedEvent(Id, userId));
    }

    public void Unassign(string userId)
    {
        AssignedToId = null;
        LastModifiedBy = userId;
        MarkAsUpdated();
    }

    public void SetDueDate(DateTime dueDate, string userId)
    {
        if (dueDate < DateTime.UtcNow)
            throw new InvalidTaskStatusException("Due date cannot be in the past");

        DueDate = dueDate;
        LastModifiedBy = userId;
        MarkAsUpdated();
    }

    public void Complete(string userId)
    {
        UpdateStatus(TaskStatus.Completed, userId);
    }

    public void Cancel(string userId)
    {
        if (Status == TaskStatus.Completed)
            throw new InvalidTaskStatusException("Cannot cancel completed task");

        Status = TaskStatus.Cancelled;
        LastModifiedBy = userId;
        MarkAsUpdated();
    }

    public bool IsOverdue() => DueDate.HasValue && DueDate.Value < DateTime.UtcNow && Status != TaskStatus.Completed;
}
```

### 3.7 Build Domain Layer

```bash
cd src/TaskManagement.Domain
dotnet build

# Should build successfully
```

---

## Step 4: Application Layer with CQRS

The Application layer implements CQRS using MediatR for clean separation of read and write operations.

### 4.1 Add NuGet Packages

```bash
cd ../TaskManagement.Application

# Add MediatR for CQRS
dotnet add package MediatR

# Add FluentValidation
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions

# Add AutoMapper
dotnet add package AutoMapper
dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection
```

### 4.2 Create Common Interfaces

**File**: `src/TaskManagement.Application/Common/Interfaces/IApplicationDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<ProjectTask> Tasks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

**File**: `src/TaskManagement.Application/Common/Interfaces/IDateTime.cs`

```csharp
namespace TaskManagement.Application.Common.Interfaces;

public interface IDateTime
{
    DateTime UtcNow { get; }
}
```

**File**: `src/TaskManagement.Application/Common/Interfaces/ICurrentUserService.cs`

```csharp
namespace TaskManagement.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
}
```

### 4.3 Create DTOs

**File**: `src/TaskManagement.Application/Common/Models/ProjectDto.cs`

```csharp
namespace TaskManagement.Application.Common.Models;

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public int ActiveTaskCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**File**: `src/TaskManagement.Application/Common/Models/TaskDto.cs`

```csharp
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Common.Models;

public class TaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public Guid ProjectId { get; set; }
    public string? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 4.4 Create AutoMapper Profile

**File**: `src/TaskManagement.Application/Common/Mappings/MappingProfile.cs`

```csharp
using AutoMapper;
using TaskManagement.Application.Common.Models;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Project, ProjectDto>()
            .ForMember(d => d.ActiveTaskCount, opt => opt.MapFrom(s => s.GetActiveTaskCount()));

        CreateMap<ProjectTask, TaskDto>()
            .ForMember(d => d.IsOverdue, opt => opt.MapFrom(s => s.IsOverdue()));
    }
}
```

### 4.5 Create Validation Behavior

**File**: `src/TaskManagement.Application/Common/Behaviours/ValidationBehaviour.cs`

```csharp
using FluentValidation;
using MediatR;

namespace TaskManagement.Application.Common.Behaviours;

public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

### 4.6 Create Commands - Projects

**File**: `src/TaskManagement.Application/Projects/Commands/CreateProject/CreateProjectCommand.cs`

```csharp
using MediatR;

namespace TaskManagement.Application.Projects.Commands.CreateProject;

public record CreateProjectCommand : IRequest<Guid>
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
```

**File**: `src/TaskManagement.Application/Projects/Commands/CreateProject/CreateProjectCommandValidator.cs`

```csharp
using FluentValidation;

namespace TaskManagement.Application.Projects.Commands.CreateProject;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(v => v.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters");
    }
}
```

**File**: `src/TaskManagement.Application/Projects/Commands/CreateProject/CreateProjectCommandHandler.cs`

```csharp
using MediatR;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Projects.Commands.CreateProject;

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateProjectCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? "system";

        var project = new Project(
            request.Name,
            request.Description,
            userId);

        _context.Projects.Add(project);

        await _context.SaveChangesAsync(cancellationToken);

        return project.Id;
    }
}
```

**File**: `src/TaskManagement.Application/Projects/Commands/UpdateProject/UpdateProjectCommand.cs`

```csharp
using MediatR;

namespace TaskManagement.Application.Projects.Commands.UpdateProject;

public record UpdateProjectCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
```

**File**: `src/TaskManagement.Application/Projects/Commands/UpdateProject/UpdateProjectCommandValidator.cs`

```csharp
using FluentValidation;

namespace TaskManagement.Application.Projects.Commands.UpdateProject;

public class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(v => v.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters");
    }
}
```

**File**: `src/TaskManagement.Application/Projects/Commands/UpdateProject/UpdateProjectCommandHandler.cs`

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;

namespace TaskManagement.Application.Projects.Commands.UpdateProject;

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateProjectCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project with ID {request.Id} not found");
        }

        var userId = _currentUserService.UserId ?? "system";

        project.UpdateDetails(request.Name, request.Description, userId);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
```

**File**: `src/TaskManagement.Application/Projects/Commands/DeleteProject/DeleteProjectCommand.cs`

```csharp
using MediatR;

namespace TaskManagement.Application.Projects.Commands.DeleteProject;

public record DeleteProjectCommand(Guid Id) : IRequest<Unit>;
```

**File**: `src/TaskManagement.Application/Projects/Commands/DeleteProject/DeleteProjectCommandHandler.cs`

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;

namespace TaskManagement.Application.Projects.Commands.DeleteProject;

public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeleteProjectCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project with ID {request.Id} not found");
        }

        _context.Projects.Remove(project);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
```

### 4.7 Create Queries - Projects

**File**: `src/TaskManagement.Application/Projects/Queries/GetProjects/GetProjectsQuery.cs`

```csharp
using MediatR;
using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Projects.Queries.GetProjects;

public record GetProjectsQuery : IRequest<List<ProjectDto>>;
```

**File**: `src/TaskManagement.Application/Projects/Queries/GetProjects/GetProjectsQueryHandler.cs`

```csharp
using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Projects.Queries.GetProjects;

public class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, List<ProjectDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetProjectsQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<List<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Projects
            .Where(p => !p.IsArchived)
            .ProjectTo<ProjectDto>(_mapper.ConfigurationProvider)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
```

**File**: `src/TaskManagement.Application/Projects/Queries/GetProjectById/GetProjectByIdQuery.cs`

```csharp
using MediatR;
using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Projects.Queries.GetProjectById;

public record GetProjectByIdQuery(Guid Id) : IRequest<ProjectDto>;
```

**File**: `src/TaskManagement.Application/Projects/Queries/GetProjectById/GetProjectByIdQueryHandler.cs`

```csharp
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Projects.Queries.GetProjectById;

public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, ProjectDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetProjectByIdQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<ProjectDto> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project with ID {request.Id} not found");
        }

        return _mapper.Map<ProjectDto>(project);
    }
}
```

### 4.8 Create Commands - Tasks

**File**: `src/TaskManagement.Application/Tasks/Commands/CreateTask/CreateTaskCommand.cs`

```csharp
using MediatR;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Tasks.Commands.CreateTask;

public record CreateTaskCommand : IRequest<Guid>
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public TaskPriority Priority { get; init; } = TaskPriority.Medium;
    public DateTime? DueDate { get; init; }
}
```

**File**: `src/TaskManagement.Application/Tasks/Commands/CreateTask/CreateTaskCommandValidator.cs`

```csharp
using FluentValidation;

namespace TaskManagement.Application.Tasks.Commands.CreateTask;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(v => v.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(v => v.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters");

        RuleFor(v => v.ProjectId)
            .NotEmpty().WithMessage("ProjectId is required");

        RuleFor(v => v.DueDate)
            .GreaterThan(DateTime.UtcNow)
            .When(v => v.DueDate.HasValue)
            .WithMessage("Due date must be in the future");
    }
}
```

**File**: `src/TaskManagement.Application/Tasks/Commands/CreateTask/CreateTaskCommandHandler.cs`

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Tasks.Commands.CreateTask;

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateTaskCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project with ID {request.ProjectId} not found");
        }

        var userId = _currentUserService.UserId ?? "system";

        var task = new ProjectTask(
            request.Title,
            request.Description,
            request.ProjectId,
            userId,
            request.Priority,
            request.DueDate);

        _context.Tasks.Add(task);

        await _context.SaveChangesAsync(cancellationToken);

        return task.Id;
    }
}
```

**File**: `src/TaskManagement.Application/Tasks/Commands/UpdateTaskStatus/UpdateTaskStatusCommand.cs`

```csharp
using MediatR;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Tasks.Commands.UpdateTaskStatus;

public record UpdateTaskStatusCommand(Guid Id, TaskStatus Status) : IRequest<Unit>;
```

**File**: `src/TaskManagement.Application/Tasks/Commands/UpdateTaskStatus/UpdateTaskStatusCommandHandler.cs`

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;

namespace TaskManagement.Application.Tasks.Commands.UpdateTaskStatus;

public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateTaskStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (task == null)
        {
            throw new KeyNotFoundException($"Task with ID {request.Id} not found");
        }

        var userId = _currentUserService.UserId ?? "system";

        task.UpdateStatus(request.Status, userId);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
```

### 4.9 Create Queries - Tasks

**File**: `src/TaskManagement.Application/Tasks/Queries/GetTasks/GetTasksQuery.cs`

```csharp
using MediatR;
using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Tasks.Queries.GetTasks;

public record GetTasksQuery(Guid? ProjectId = null) : IRequest<List<TaskDto>>;
```

**File**: `src/TaskManagement.Application/Tasks/Queries/GetTasks/GetTasksQueryHandler.cs`

```csharp
using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Common.Models;

namespace TaskManagement.Application.Tasks.Queries.GetTasks;

public class GetTasksQueryHandler : IRequestHandler<GetTasksQuery, List<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetTasksQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<List<TaskDto>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Tasks.AsQueryable();

        if (request.ProjectId.HasValue)
        {
            query = query.Where(t => t.ProjectId == request.ProjectId.Value);
        }

        return await query
            .ProjectTo<TaskDto>(_mapper.ConfigurationProvider)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
```

### 4.10 Create DependencyInjection Class

**File**: `src/TaskManagement.Application/DependencyInjection.cs`

```csharp
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Common.Behaviours;

namespace TaskManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        return services;
    }
}
```

### 4.11 Build Application Layer

```bash
dotnet build

# Should build successfully
```

---

## Step 5: Infrastructure Layer

The Infrastructure layer implements interfaces defined in the Application layer.

### 5.1 Add NuGet Packages

```bash
cd ../TaskManagement.Infrastructure

# Entity Framework Core for PostgreSQL
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Tools

# Microsoft Extensions
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Options.ConfigurationExtensions
```

### 5.2 Create DbContext

**File**: `src/TaskManagement.Infrastructure/Persistence/ApplicationDbContext.cs`

```csharp
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Domain.Common;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService,
        IDateTime dateTime) : base(options)
    {
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.GetType()
                        .GetProperty(nameof(BaseAuditableEntity.CreatedBy))
                        ?.SetValue(entry.Entity, _currentUserService.UserId ?? "system");
                    break;

                case EntityState.Modified:
                    entry.Entity.GetType()
                        .GetProperty(nameof(BaseAuditableEntity.LastModifiedBy))
                        ?.SetValue(entry.Entity, _currentUserService.UserId ?? "system");
                    break;
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch domain events (simplified - could use more sophisticated approach)
        await DispatchDomainEvents();

        return result;
    }

    private async Task DispatchDomainEvents()
    {
        var domainEntities = ChangeTracker
            .Entries<BaseEntity>()
            .Where(x => x.Entity.DomainEvents.Any())
            .Select(x => x.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(x => x.DomainEvents)
            .ToList();

        domainEntities.ForEach(entity => entity.ClearDomainEvents());

        // Here you would typically publish domain events
        // For now, we'll just clear them
        await Task.CompletedTask;
    }
}
```

### 5.3 Create Entity Configurations

**File**: `src/TaskManagement.Infrastructure/Persistence/Configurations/ProjectConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.OwnerId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.LastModifiedBy)
            .HasMaxLength(100);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.HasMany(p => p.Tasks)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(p => p.DomainEvents);

        builder.HasIndex(p => p.OwnerId);
        builder.HasIndex(p => p.CreatedAt);
    }
}
```

**File**: `src/TaskManagement.Infrastructure/Persistence/Configurations/ProjectTaskConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Infrastructure.Persistence.Configurations;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("Tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.AssignedToId)
            .HasMaxLength(100);

        builder.Property(t => t.CreatedBy)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.LastModifiedBy)
            .HasMaxLength(100);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Ignore(t => t.DomainEvents);

        builder.HasIndex(t => t.ProjectId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.AssignedToId);
        builder.HasIndex(t => t.CreatedAt);
    }
}
```

### 5.4 Create Services

**File**: `src/TaskManagement.Infrastructure/Services/DateTimeService.cs`

```csharp
using TaskManagement.Application.Common.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

### 5.5 Create DependencyInjection Class

**File**: `src/TaskManagement.Infrastructure/DependencyInjection.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services.AddTransient<IDateTime, DateTimeService>();

        return services;
    }
}
```

### 5.6 Build Infrastructure Layer

```bash
dotnet build

# Should build successfully
```

---

## Step 6: Web API Layer

### 6.1 Add NuGet Packages

```bash
cd ../TaskManagement.WebApi

# Add required packages
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Swashbuckle.AspNetCore
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
```

### 6.2 Create CurrentUserService

**File**: `src/TaskManagement.WebApi/Services/CurrentUserService.cs`

```csharp
using System.Security.Claims;
using TaskManagement.Application.Common.Interfaces;

namespace TaskManagement.WebApi.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");

    public string? UserName => _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
```

### 6.3 Create Exception Middleware

**File**: `src/TaskManagement.WebApi/Middleware/ExceptionHandlingMiddleware.cs`

```csharp
using System.Net;
using System.Text.Json;
using FluentValidation;

namespace TaskManagement.WebApi.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An error occurred: {Message}", exception.Message);

        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Message = exception.Message,
            StatusCode = (int)HttpStatusCode.InternalServerError
        };

        switch (exception)
        {
            case ValidationException validationException:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Message = "Validation failed";
                errorResponse.Errors = validationException.Errors
                    .Select(e => $"{e.PropertyName}: {e.ErrorMessage}")
                    .ToList();
                break;

            case KeyNotFoundException:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.StatusCode = (int)HttpStatusCode.NotFound;
                break;

            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Message = "An internal server error occurred";
                break;
        }

        var result = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(result);
    }

    private class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public List<string>? Errors { get; set; }
    }
}
```

### 6.4 Create Controllers

**File**: `src/TaskManagement.WebApi/Controllers/ProjectsController.cs`

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Projects.Commands.CreateProject;
using TaskManagement.Application.Projects.Commands.DeleteProject;
using TaskManagement.Application.Projects.Commands.UpdateProject;
using TaskManagement.Application.Projects.Queries.GetProjectById;
using TaskManagement.Application.Projects.Queries.GetProjects;

namespace TaskManagement.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IMediator mediator, ILogger<ProjectsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all projects
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjects()
    {
        var projects = await _mediator.Send(new GetProjectsQuery());
        return Ok(projects);
    }

    /// <summary>
    /// Get project by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var project = await _mediator.Send(new GetProjectByIdQuery(id));
        return Ok(project);
    }

    /// <summary>
    /// Create a new project
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectCommand command)
    {
        var projectId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetProject), new { id = projectId }, new { id = projectId });
    }

    /// <summary>
    /// Update an existing project
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("ID mismatch");
        }

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Delete a project
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        await _mediator.Send(new DeleteProjectCommand(id));
        return NoContent();
    }
}
```

**File**: `src/TaskManagement.WebApi/Controllers/TasksController.cs`

```csharp
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Tasks.Commands.CreateTask;
using TaskManagement.Application.Tasks.Commands.UpdateTaskStatus;
using TaskManagement.Application.Tasks.Queries.GetTasks;

namespace TaskManagement.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TasksController> _logger;

    public TasksController(IMediator mediator, ILogger<TasksController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all tasks, optionally filtered by project
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasks([FromQuery] Guid? projectId = null)
    {
        var tasks = await _mediator.Send(new GetTasksQuery(projectId));
        return Ok(tasks);
    }

    /// <summary>
    /// Create a new task
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskCommand command)
    {
        var taskId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetTasks), new { id = taskId }, new { id = taskId });
    }

    /// <summary>
    /// Update task status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTaskStatus(Guid id, [FromBody] UpdateTaskStatusRequest request)
    {
        await _mediator.Send(new UpdateTaskStatusCommand(id, request.Status));
        return NoContent();
    }
}

public record UpdateTaskStatusRequest(TaskManagement.Domain.Enums.TaskStatus Status);
```

### 6.5 Update Program.cs

**File**: `src/TaskManagement.WebApi/Program.cs`

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using TaskManagement.Application;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Infrastructure;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.WebApi.Middleware;
using TaskManagement.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Task Management API",
        Version = "v1",
        Description = "Enterprise CRUD API with Clean Architecture, CQRS, and OAuth2"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add application services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = false; // Set to true in production

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Error("Authentication failed: {Message}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Information("Token validated for user: {User}",
                    context.Principal?.Identity?.Name ?? "Unknown");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Task Management API V1");
    });

    // Auto-apply migrations in development
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}

app.UseSerilogRequestLogging();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
```

### 6.6 Update appsettings.json

**File**: `src/TaskManagement.WebApi/appsettings.json`

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=taskmanagement;Username=postgres;Password=postgres"
  },
  "Authentication": {
    "Authority": "http://localhost:8080/realms/taskmanagement",
    "Audience": "taskmanagement-api"
  },
  "AllowedHosts": "*"
}
```

### 6.7 Build Web API

```bash
dotnet build
```

---

## Step 7: .NET Aspire Integration

### 7.1 Update AppHost Project

**File**: `TaskManagement.AppHost/Program.cs`

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var postgresDb = postgres.AddDatabase("taskmanagementdb");

// Add Keycloak for authentication (optional - see Step 9)
// var keycloak = builder.AddKeycloak("keycloak", 8080);

// Add the Web API project
var api = builder.AddProject<Projects.TaskManagement_WebApi>("taskmanagement-api")
    .WithReference(postgresDb);

builder.Build().Run();
```

### 7.2 Add Project Reference to AppHost

```bash
cd ../../TaskManagement.AppHost
dotnet add reference ../src/TaskManagement.WebApi/TaskManagement.WebApi.csproj
```

---

## Step 8: PostgreSQL Setup

### 8.1 Option 1: Using .NET Aspire (Recommended)

When you run the AppHost, Aspire will automatically start PostgreSQL in Docker.

```bash
# From solution root
cd TaskManagement.AppHost
dotnet run
```

This will:
1. Start PostgreSQL container
2. Start the Web API
3. Open Aspire Dashboard (usually at http://localhost:15000)

### 8.2 Option 2: Manual Docker Setup

If you prefer manual setup:

```bash
# Run PostgreSQL container
docker run --name taskmanagement-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_DB=taskmanagement \
  -p 5432:5432 \
  -d postgres:16

# Verify it's running
docker ps
```

### 8.3 Create Database Migration

```bash
cd src/TaskManagement.Infrastructure

# Create initial migration
dotnet ef migrations add InitialCreate \
  --startup-project ../TaskManagement.WebApi/TaskManagement.WebApi.csproj \
  --output-dir Persistence/Migrations

# Apply migration (or let Program.cs do it automatically)
dotnet ef database update \
  --startup-project ../TaskManagement.WebApi/TaskManagement.WebApi.csproj
```

---

## Step 9: OAuth2/OpenID Connect Security

### 9.1 Understanding OAuth2/OIDC

**OAuth2** is an authorization framework that enables applications to obtain limited access to user accounts.

**OpenID Connect (OIDC)** is an identity layer on top of OAuth2 that adds authentication.

**Flow**:
1. User authenticates with Identity Provider (IdP)
2. IdP issues a JWT (JSON Web Token)
3. Client sends token with each API request
4. API validates token and authorizes access

### 9.2 Option 1: Keycloak Setup (Recommended for Learning)

Keycloak is a free, open-source identity and access management solution.

**Step 1: Run Keycloak with Docker**

```bash
docker run -d \
  --name keycloak \
  -p 8080:8080 \
  -e KEYCLOAK_ADMIN=admin \
  -e KEYCLOAK_ADMIN_PASSWORD=admin \
  quay.io/keycloak/keycloak:23.0 \
  start-dev
```

**Step 2: Access Keycloak Admin Console**

- URL: http://localhost:8080
- Username: `admin`
- Password: `admin`

**Step 3: Create Realm**

1. Click on dropdown at top-left (currently shows "master")
2. Click "Create Realm"
3. Name: `taskmanagement`
4. Click "Create"

**Step 4: Create Client**

1. Navigate to "Clients" in left sidebar
2. Click "Create client"
3. Client ID: `taskmanagement-api`
4. Client Protocol: `openid-connect`
5. Click "Next"
6. Enable "Client authentication"
7. Enable "Authorization"
8. Enable "Service accounts roles"
9. Enable "Standard flow"
10. Enable "Direct access grants"
11. Click "Save"

**Step 5: Configure Client**

1. Go to "Settings" tab
2. Valid redirect URIs: `http://localhost:5000/*`
3. Web Origins: `http://localhost:5000`
4. Click "Save"

**Step 6: Get Client Secret**

1. Go to "Credentials" tab
2. Copy the "Client Secret" value
3. You'll need this for testing

**Step 7: Create Test User**

1. Navigate to "Users" in left sidebar
2. Click "Add user"
3. Username: `testuser`
4. Email: `test@example.com`
5. First name: `Test`
6. Last name: `User`
7. Email verified: ON
8. Click "Create"
9. Go to "Credentials" tab
10. Click "Set password"
11. Password: `password123`
12. Temporary: OFF
13. Click "Save"

**Step 8: Update appsettings.json**

Update your WebApi appsettings.json:

```json
{
  "Authentication": {
    "Authority": "http://localhost:8080/realms/taskmanagement",
    "Audience": "taskmanagement-api"
  }
}
```

### 9.3 Option 2: Azure AD / Entra ID

If you have an Azure subscription:

1. Create an App Registration in Azure Portal
2. Configure API permissions
3. Update appsettings.json:

```json
{
  "Authentication": {
    "Authority": "https://login.microsoftonline.com/{tenant-id}",
    "Audience": "{client-id}"
  }
}
```

### 9.4 Testing Authentication

**Get Access Token (Keycloak)**

```bash
curl -X POST http://localhost:8080/realms/taskmanagement/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=taskmanagement-api" \
  -d "client_secret=YOUR_CLIENT_SECRET" \
  -d "username=testuser" \
  -d "password=password123" \
  -d "grant_type=password"
```

Save the `access_token` from the response.

---

## Step 10: Testing the API

### 10.1 Start the Application

**Using Aspire (Recommended)**:

```bash
cd TaskManagement.AppHost
dotnet run
```

This opens the Aspire Dashboard where you can:
- See all running services
- View logs
- Check health status
- View traces

**Or start API directly**:

```bash
cd src/TaskManagement.WebApi
dotnet run
```

### 10.2 Access Swagger UI

Open: https://localhost:7000/swagger (or the port shown in terminal)

### 10.3 Authenticate in Swagger

1. Click "Authorize" button (top right)
2. Enter: `Bearer YOUR_ACCESS_TOKEN`
3. Click "Authorize"

### 10.4 Test Endpoints

**Create a Project**:

```json
POST /api/v1/projects
{
  "name": "My First Project",
  "description": "Learning Enterprise CRUD"
}
```

**Get All Projects**:

```
GET /api/v1/projects
```

**Create a Task**:

```json
POST /api/v1/tasks
{
  "title": "Implement authentication",
  "description": "Add OAuth2 authentication",
  "projectId": "YOUR_PROJECT_ID",
  "priority": 2,
  "dueDate": "2026-04-15T00:00:00Z"
}
```

**Update Task Status**:

```json
PATCH /api/v1/tasks/{taskId}/status
{
  "status": 1
}
```

### 10.5 Test with Postman

**Step 1: Get Token**

```
POST http://localhost:8080/realms/taskmanagement/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

client_id=taskmanagement-api
&client_secret=YOUR_CLIENT_SECRET
&username=testuser
&password=password123
&grant_type=password
```

**Step 2: Use Token in Requests**

```
GET https://localhost:7000/api/v1/projects
Authorization: Bearer YOUR_ACCESS_TOKEN
```

### 10.6 Explore Aspire Dashboard

The Aspire Dashboard shows:
- **Resources**: All running services (API, PostgreSQL, etc.)
- **Logs**: Centralized logging from all services
- **Traces**: Distributed tracing for requests
- **Metrics**: Performance metrics

---

## Step 11: Next Steps & Exercises

### Congratulations!

You've built a production-ready Enterprise CRUD API with:
- ✅ Clean Architecture
- ✅ CQRS with MediatR
- ✅ OAuth2/OpenID Connect authentication
- ✅ .NET Aspire orchestration
- ✅ PostgreSQL database
- ✅ Validation, error handling, logging

### Exercises to Extend Your Learning

1. **Add Caching**
   - Install Redis via Aspire
   - Cache query results
   - Implement cache invalidation

2. **Add More Features**
   - Comments on tasks
   - File attachments
   - Task dependencies
   - Time tracking

3. **Improve Security**
   - Role-based authorization
   - Resource-based authorization
   - API rate limiting

4. **Add Testing**
   - Unit tests for domain logic
   - Integration tests for API
   - Test authentication/authorization

5. **Add Notifications**
   - Email notifications
   - Real-time updates with SignalR
   - Background jobs with Hangfire

6. **Performance Optimization**
   - Query optimization
   - Pagination
   - Response compression

7. **Deployment**
   - Containerize with Docker
   - Deploy to Azure
   - Set up CI/CD

---

## Troubleshooting

### Issue: Migrations fail

```bash
# Delete migrations folder
rm -rf src/TaskManagement.Infrastructure/Persistence/Migrations

# Recreate migration
cd src/TaskManagement.Infrastructure
dotnet ef migrations add InitialCreate \
  --startup-project ../TaskManagement.WebApi/TaskManagement.WebApi.csproj
```

### Issue: PostgreSQL connection fails

Check:
1. Docker container is running: `docker ps`
2. Connection string is correct
3. Port 5432 is not in use

### Issue: Authentication fails

1. Verify Keycloak is running: http://localhost:8080
2. Check Authority URL in appsettings.json
3. Ensure token is not expired
4. Check Bearer token format: `Bearer YOUR_TOKEN`

### Issue: Aspire won't start

1. Ensure Docker is running
2. Update Aspire workload: `dotnet workload update`
3. Check port availability (5000-5005, 15000)

---

## Summary

You've learned to:
- Structure applications using Clean Architecture
- Implement CQRS with MediatR
- Secure APIs with OAuth2/OIDC
- Use .NET Aspire for orchestration
- Work with PostgreSQL and EF Core
- Apply Dependency Injection patterns
- Build production-ready APIs

**Time to build something amazing!** 🚀
