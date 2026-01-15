# Module 6: Cloud Native Development

## Overview
Learn to design and build applications optimized for cloud environments. Master microservices, containerization, health checks, and cloud-native patterns following the 12-factor app principles.

## Learning Objectives
- Build microservices with .NET
- Containerize applications with Docker
- Implement health checks and observability
- Manage configuration and secrets
- Apply 12-factor app principles
- Design for resilience and scalability

## Projects

### Microservices
**What you'll learn:**
- Microservices architecture principles
- Service boundaries and decomposition
- API Gateway pattern
- Service-to-service communication
- Docker and Docker Compose
- Kubernetes basics (optional)

**Exercises:**
1. Decompose monolith into microservices
2. Build API Gateway with YARP
3. Implement service discovery
4. Container orchestration with Docker Compose

### HealthChecks
**What you'll learn:**
- Health check patterns
- Liveness vs readiness probes
- Dependency health checks
- Custom health checks
- Health check UI

**Exercises:**
1. Implement database health checks
2. Add external service health checks
3. Create custom health check logic
4. Build health check dashboard

### Configuration
**What you'll learn:**
- External configuration management
- Environment-specific settings
- Azure Key Vault / AWS Secrets Manager
- Configuration providers
- Options pattern in .NET

**Exercises:**
1. Externalize all configuration
2. Implement secrets management
3. Use Azure App Configuration
4. Handle configuration updates dynamically

## Key Concepts

### The 12-Factor App

#### 1. Codebase
One codebase tracked in version control, many deploys
```bash
# Single repo, multiple environments
git push origin main
# Deploys to dev, staging, production
```

#### 2. Dependencies
Explicitly declare and isolate dependencies
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
```

#### 3. Config
Store config in environment variables
```csharp
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
var apiKey = configuration["ExternalAPI:ApiKey"];
```

#### 4. Backing Services
Treat backing services as attached resources
```csharp
// Database URL from config, can swap easily
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("Database")));
```

#### 5. Build, Release, Run
Strictly separate build and run stages
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet build

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY --from=build /src/out .
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

#### 6. Processes
Execute as one or more stateless processes
- No local state
- Use external stores (Redis, databases)
- Enable horizontal scaling

#### 7. Port Binding
Export services via port binding
```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run(); // Binds to port from ASPNETCORE_URLS
```

#### 8. Concurrency
Scale out via the process model
```bash
# Scale horizontally
docker-compose up --scale api=5
```

#### 9. Disposability
Maximize robustness with fast startup and graceful shutdown
```csharp
// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    // Clean up resources
});
```

#### 10. Dev/Prod Parity
Keep development, staging, and production as similar as possible
- Use same databases in all environments
- Same backing services
- Containerization helps

#### 11. Logs
Treat logs as event streams
```csharp
// Write to stdout/stderr, not files
logger.LogInformation("Order {OrderId} processed", orderId);
```

#### 12. Admin Processes
Run admin/management tasks as one-off processes
```bash
dotnet ef database update # Migration
dotnet MyApp.dll seed-data # Data seeding
```

### Microservices Architecture

#### Service Boundaries
```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Order     │────▶│  Inventory  │     │   Payment   │
│  Service    │     │   Service   │◀────│   Service   │
└─────────────┘     └─────────────┘     └─────────────┘
       │                    │                    │
       └────────────────────┴────────────────────┘
                           │
                    ┌─────────────┐
                    │    Event    │
                    │     Bus     │
                    └─────────────┘
```

#### Communication Patterns
1. **Synchronous**: HTTP/gRPC
2. **Asynchronous**: Message queues, events
3. **API Gateway**: Single entry point

```csharp
// Synchronous with HTTP
public class OrderService
{
    private readonly HttpClient _inventoryClient;

    public async Task<bool> CheckInventory(Guid productId)
    {
        var response = await _inventoryClient
            .GetAsync($"/api/inventory/{productId}");
        return response.IsSuccessStatusCode;
    }
}

// Asynchronous with events
public class OrderService
{
    private readonly IMessageBus _bus;

    public async Task PlaceOrder(Order order)
    {
        await _orderRepo.SaveAsync(order);
        await _bus.PublishAsync(new OrderPlacedEvent(order));
    }
}
```

### Docker & Containerization

#### Dockerfile Best Practices
```dockerfile
# Multi-stage build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore (layer caching)
COPY ["MyApp/MyApp.csproj", "MyApp/"]
RUN dotnet restore "MyApp/MyApp.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/MyApp"
RUN dotnet build "MyApp.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "MyApp.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Non-root user
USER app
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

#### Docker Compose
```yaml
version: '3.8'
services:
  api:
    build: ./OrderService
    ports:
      - "5000:80"
    environment:
      - DATABASE_URL=postgresql://db:5432/orders
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      - db

  db:
    image: postgres:15
    environment:
      POSTGRES_PASSWORD: password
    volumes:
      - postgres-data:/var/lib/postgresql/data

volumes:
  postgres-data:
```

### Health Checks

#### ASP.NET Core Health Checks
```csharp
// Startup
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddUrlGroup(new Uri("https://api.external.com"), name: "external-api")
    .AddCheck<CustomHealthCheck>("custom");

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// Custom Health Check
public class CustomHealthCheck : IHealthCheck
{
    private readonly IExternalService _service;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _service.PingAsync();
            return isHealthy
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Service is down");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Exception occurred", ex);
        }
    }
}
```

#### Kubernetes Probes
```yaml
apiVersion: v1
kind: Pod
spec:
  containers:
  - name: api
    image: myapi:latest
    livenessProbe:
      httpGet:
        path: /health/live
        port: 80
      initialDelaySeconds: 30
      periodSeconds: 10
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 80
      initialDelaySeconds: 5
      periodSeconds: 5
```

### Configuration Management

#### Options Pattern
```csharp
// appsettings.json
{
  "ExternalApi": {
    "BaseUrl": "https://api.example.com",
    "ApiKey": "key-from-keyvault",
    "Timeout": 30
  }
}

// Options class
public class ExternalApiOptions
{
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
    public int Timeout { get; set; }
}

// Registration
builder.Services.Configure<ExternalApiOptions>(
    builder.Configuration.GetSection("ExternalApi"));

// Usage
public class MyService
{
    private readonly ExternalApiOptions _options;

    public MyService(IOptions<ExternalApiOptions> options)
    {
        _options = options.Value;
    }
}
```

#### Azure Key Vault
```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential());

// Access secrets
var secret = builder.Configuration["MySecret"];
```

## Observability

### Logging
```csharp
// Structured logging
logger.LogInformation(
    "Order {OrderId} placed by customer {CustomerId} with total {Total:C}",
    order.Id, customer.Id, order.Total);
```

### Metrics
```csharp
// Application Insights
services.AddApplicationInsightsTelemetry();

// Custom metrics
var meter = new Meter("MyApp");
var orderCounter = meter.CreateCounter<int>("orders_placed");
orderCounter.Add(1, new KeyValuePair<string, object>("customer", customerId));
```

### Distributed Tracing
```csharp
// OpenTelemetry
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddJaegerExporter());
```

## Best Practices

### Microservices
1. **Design around business capabilities**
2. **Decentralize data management**
3. **Design for failure**
4. **Use API versioning**
5. **Implement circuit breakers**
6. **Monitor everything**

### Containers
1. **Use multi-stage builds**
2. **Minimize image size**
3. **Don't run as root**
4. **Use specific image tags**
5. **Scan for vulnerabilities**

### Configuration
1. **Never commit secrets**
2. **Use managed secret stores**
3. **Validate configuration at startup**
4. **Support configuration reload**
5. **Document all settings**

## Exercises

### Exercise 1: Microservices System
Build a distributed e-commerce system:
1. Order Service
2. Inventory Service
3. Payment Service
4. API Gateway
5. Message bus for communication

### Exercise 2: Containerization
1. Dockerize all services
2. Create docker-compose.yml
3. Add health checks
4. Implement graceful shutdown

### Exercise 3: Observability
1. Add structured logging
2. Implement distributed tracing
3. Create custom metrics
4. Build monitoring dashboard

### Exercise 4: Configuration
1. Externalize all config
2. Use Azure Key Vault for secrets
3. Support multiple environments
4. Implement feature flags

## Prerequisites
- Modules 1-5 completed
- Docker installed
- Azure account (for cloud exercises)
- Understanding of HTTP and REST

## Getting Started
1. Install Docker Desktop
2. Start with [Microservices](Microservices/)
3. Progress to [HealthChecks](HealthChecks/)
4. Complete with [Configuration](Configuration/)

## Next Module
After completing this module, proceed to [07-ArchitecturePatterns](../07-ArchitecturePatterns/)
