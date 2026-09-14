using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskManagement.Application;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Infrastructure;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.WebApi.Endpoints;
using TaskManagement.WebApi.Middleware;
using TaskManagement.WebApi.Security;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuration ----------

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddSingleton(jwtOptions);

// ---------- Layers ----------

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ICurrentUser is implemented HERE, not in Infrastructure, because the caller's
// identity is an HTTP concern. Infrastructure supplies a SystemUser for jobs.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<DevelopmentTokenIssuer>();

// ---------- Authentication ----------

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (!string.IsNullOrWhiteSpace(jwtOptions.Authority))
        {
            // Production: validate against a real OIDC provider (Keycloak,
            // Entra ID, Auth0). Keys are fetched from its published JWKS, so
            // this application never holds a signing secret.
            options.Authority = jwtOptions.Authority;
            options.Audience = jwtOptions.Audience;

            // JwtBearer refuses a non-HTTPS authority unless this is explicitly
            // turned off, and it is right to: the authority is where signing
            // keys come from, so fetching them over plaintext lets anyone on
            // the path hand you their own keys and mint valid tokens.
            //
            // A local Keycloak on http://localhost:8080 therefore needs the
            // override -- and it is gated on Development so it can never ship.
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RoleClaimType = "roles"
            };
        }
        else
        {
            // Development: symmetric key, so the API runs without an identity
            // provider. See /dev/token.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        }
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("admin", policy => policy.RequireRole("admin"));
});

// ---------- Cross-cutting ----------

builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Task Management API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste a token from POST /dev/token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

var app = builder.Build();

// ---------- Pipeline ----------

app.UseExceptionHandler();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Development-only token endpoint, so the API is usable without standing
    // up Keycloak first. Registered only in Development -- there is no way to
    // reach it in any other environment.
    app.MapPost("/dev/token", (DevelopmentTokenIssuer issuer, string? userId, string? roles) =>
    {
        var id = userId ?? "dev-user";
        var roleList = (roles ?? "user").Split(',', StringSplitOptions.RemoveEmptyEntries);

        return Results.Ok(new
        {
            access_token = issuer.Issue(id, id, roleList),
            token_type = "Bearer",
            expires_in = 3600,
            roles = roleList
        });
    }).WithTags("Development");
}

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapGet("/", () => Results.Ok(new
{
    service = "Task Management API",
    version = "v1",
    documentation = "/swagger",
    authentication = "POST /dev/token (Development only), then send: Authorization: Bearer <token>",
    endpoints = new[]
    {
        "POST   /api/v1/projects",
        "GET    /api/v1/projects?status=&ownerId=&page=&pageSize=",
        "GET    /api/v1/projects/{id}",
        "PUT    /api/v1/projects/{id}",
        "POST   /api/v1/projects/{id}/archive",
        "DELETE /api/v1/projects/{id}",
        "POST   /api/v1/projects/{id}/tasks",
        "GET    /api/v1/tasks?projectId=&state=&minimumPriority=&assigneeId=&overdueOnly=",
        "GET    /api/v1/tasks/{id}",
        "PUT    /api/v1/tasks/{id}",
        "POST   /api/v1/tasks/{id}/assign",
        "POST   /api/v1/tasks/{id}/move",
        "DELETE /api/v1/tasks/{id}"
    }
}));

app.MapApi();

app.Run();

// Exposed so the integration tests can host this with WebApplicationFactory.
public partial class Program;
