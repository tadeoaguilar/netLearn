using AdvancedDI.Configuration;
using AdvancedDI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdvancedDI.Demos;

public static class Part3Options
{
    public static void Run()
    {
        Console.WriteLine("=== PART 3: CONFIGURATION BINDING ===\n");

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile("appsettings.json", optional: false);

        builder.Services.Configure<AppSettings>(builder.Configuration);
        builder.Services.Configure<DatabaseSettings>(
            builder.Configuration.GetSection(DatabaseSettings.SectionName));
        builder.Services.Configure<EmailSettings>(
            builder.Configuration.GetSection(EmailSettings.SectionName));

        builder.Services.AddTransient<ConfigurationConsumer>();

        var host = builder.Build();
        host.Services.GetRequiredService<ConfigurationConsumer>().DisplayConfiguration();
    }
}
