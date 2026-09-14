using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Picks the provider from configuration, so the SAME build runs on SQLite
    /// locally and PostgreSQL in a container -- the 12-factor rule from
    /// module 06 applied to the database.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue("Database:Provider", "sqlite")!.ToLowerInvariant();
        var connectionString = configuration.GetConnectionString("Default")
                               ?? "Data Source=taskmanagement.db";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            switch (provider)
            {
                case "postgres" or "postgresql":
                    options.UseNpgsql(connectionString);
                    break;
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown Database:Provider '{provider}'. Use 'sqlite' or 'postgres'.");
            }
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        return services;
    }
}
