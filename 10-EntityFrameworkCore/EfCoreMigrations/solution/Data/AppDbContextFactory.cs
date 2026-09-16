using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EfCoreMigrations.Data;

/// <summary>
/// Lets `dotnet ef` commands build an AppDbContext at design time without
/// spinning up the whole host (no ASP.NET Core pipeline, no DI container
/// wiring for unrelated services). `dotnet ef migrations add` and
/// `dotnet ef database update` both find this automatically because it
/// implements IDesignTimeDbContextFactory&lt;AppDbContext&gt; and lives in
/// this assembly -- no extra configuration needed.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=efcore_migrations;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
