using AdvancedDI.Configuration;
using Microsoft.Extensions.Options;

namespace AdvancedDI.Services;

/// <summary>
/// Part 3: depends on <see cref="IOptions{T}"/> rather than IConfiguration, so
/// it never performs string lookups and can be tested with Options.Create(...).
/// </summary>
public class ConfigurationConsumer
{
    private readonly AppSettings _appSettings;
    private readonly DatabaseSettings _dbSettings;
    private readonly EmailSettings _emailSettings;

    public ConfigurationConsumer(
        IOptions<AppSettings> appSettings,
        IOptions<DatabaseSettings> dbSettings,
        IOptions<EmailSettings> emailSettings)
    {
        _appSettings = appSettings.Value;
        _dbSettings = dbSettings.Value;
        _emailSettings = emailSettings.Value;
    }

    public void DisplayConfiguration()
    {
        Console.WriteLine("=== APPLICATION CONFIGURATION ===\n");

        Console.WriteLine($"App Name: {_appSettings.ApplicationName}");
        Console.WriteLine($"Environment: {_appSettings.Environment}\n");

        Console.WriteLine("Database Settings:");
        Console.WriteLine($"  Connection: {_dbSettings.ConnectionString}");
        Console.WriteLine($"  Max Retries: {_dbSettings.MaxRetries}");
        Console.WriteLine($"  Timeout: {_dbSettings.TimeoutSeconds}s\n");

        Console.WriteLine("Email Settings:");
        Console.WriteLine($"  SMTP Server: {_emailSettings.SmtpServer}");
        Console.WriteLine($"  Port: {_emailSettings.Port}");
        Console.WriteLine($"  From: {_emailSettings.FromAddress}");
        Console.WriteLine($"  SSL: {_emailSettings.UseSsl}");
    }
}
