using AdvancedDI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdvancedDI.Demos;

public static class Part5Conditional
{
    public static void Run(string[] args)
    {
        Console.WriteLine("=== PART 5: CONDITIONAL REGISTRATION ===\n");

        var builder = Host.CreateApplicationBuilder(args);

        Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}\n");

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
        }
        else
        {
            builder.Services.AddSingleton<ICacheService, RedisCacheService>();
        }

        var host = builder.Build();

        var cache = host.Services.GetRequiredService<ICacheService>();
        cache.Set("user:1", "John Doe");
        cache.Get("user:1");
        cache.Get("user:2");

        Console.WriteLine("\nRe-run with a different environment to swap the implementation:");
        Console.WriteLine("  DOTNET_ENVIRONMENT=Production dotnet run -- 5");
    }
}
