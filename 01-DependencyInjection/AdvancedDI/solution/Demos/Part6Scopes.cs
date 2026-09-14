using AdvancedDI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdvancedDI.Demos;

public static class Part6Scopes
{
    public static void Run()
    {
        Console.WriteLine("=== PART 6: SERVICE PROVIDER SCOPES ===\n");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddScoped<IDatabaseConnection, DatabaseConnection>();
        builder.Services.AddTransient<UnitOfWork>();

        var host = builder.Build();

        Console.WriteLine("Creating Scope 1:");
        using (var scope1 = host.Services.CreateScope())
        {
            var unitOfWork1 = scope1.ServiceProvider.GetRequiredService<UnitOfWork>();
            unitOfWork1.DoWork("Create User");

            var unitOfWork2 = scope1.ServiceProvider.GetRequiredService<UnitOfWork>();
            unitOfWork2.DoWork("Update Profile");

            Console.WriteLine($"\nTwo units of work, one connection? " +
                              $"{unitOfWork1.ConnectionId == unitOfWork2.ConnectionId}");
        }

        Console.WriteLine("\nCreating Scope 2:");
        using (var scope2 = host.Services.CreateScope())
        {
            var unitOfWork3 = scope2.ServiceProvider.GetRequiredService<UnitOfWork>();
            unitOfWork3.DoWork("Delete Record");
        }

        Console.WriteLine("\nNotice each connection closed when its scope ended,");
        Console.WriteLine("not when the last consumer finished with it.");
    }
}
