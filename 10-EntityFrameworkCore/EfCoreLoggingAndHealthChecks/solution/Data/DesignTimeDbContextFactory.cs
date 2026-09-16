using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EfCoreLoggingAndHealthChecks.Data;

/// <summary>
/// Lets `dotnet ef migrations add` / `dotnet ef database update` build an
/// <see cref="AppDbContext"/> WITHOUT running the whole minimal-API pipeline
/// in Program.cs (health checks, interceptors, etc.) and without needing a
/// live database just to author a migration -- Npgsql only validates the
/// connection string's shape for that, not connectivity.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=efcore_healthchecks;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
