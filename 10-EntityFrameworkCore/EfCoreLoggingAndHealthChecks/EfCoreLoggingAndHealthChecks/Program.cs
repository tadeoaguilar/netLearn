var builder = WebApplication.CreateBuilder(args);

// Work through EXERCISE.md and build this up part by part:
//   Part 1 - simple LogTo() logging, narrowed to Database.Command events
//   Part 2 - EnableSensitiveDataLogging()/EnableDetailedErrors(), dev-only
//   Part 3 - a SaveChangesInterceptor that stamps CreatedAt/UpdatedAt
//   Part 4 - a DbCommandInterceptor that flags slow queries
//   Part 5 - wire EF Core's logs through the app's ILoggerFactory
//   Part 6 - /health/ready and /health/live
//   Part 7 - a custom health check for pending migrations
//
// builder.Services.AddDbContext<AppDbContext>(...);
// builder.Services.AddHealthChecks()...

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "EfCoreLoggingAndHealthChecks (workspace)" }));

app.Run();

public partial class Program;
