using CloudNative.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// 12-factor configuration: the SAME build runs in every environment, and only
// the environment differs.
//
//     dotnet run
//     DOTNET_ENVIRONMENT=Production dotnet run
//     Database__Host=from-env dotnet run
//     dotnet run -- --Database:MaxPoolSize=5

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

Console.WriteLine($"=== CONFIGURATION ({environment}) ===\n");

// Later sources WIN. This ordering is the whole mechanism:
//   base file  <  environment file  <  environment variables  <  command line
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var services = new ServiceCollection();

services.AddOptions<DatabaseSettings>()
    .Bind(configuration.GetSection(DatabaseSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();   // fail at boot, not on first use

services.Configure<FeatureFlags>(configuration.GetSection(FeatureFlags.SectionName));

var provider = services.BuildServiceProvider();

// ValidateOnStart only runs when something asks for the options, so a real
// host does this for you at startup. Here we trigger it explicitly.
try
{
    var validator = provider.GetRequiredService<IOptions<DatabaseSettings>>();
    _ = validator.Value;
}
catch (OptionsValidationException ex)
{
    Console.WriteLine("Configuration is invalid -- refusing to start:\n");
    foreach (var failure in ex.Failures) Console.WriteLine($"  {failure}");
    return 1;
}

var database = provider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
var features = provider.GetRequiredService<IOptions<FeatureFlags>>().Value;

Console.WriteLine($"Service:     {configuration["ServiceName"]}");
Console.WriteLine($"Connection:  {database.ToConnectionString()}");
Console.WriteLine($"Pool size:   {database.MaxPoolSize}");
Console.WriteLine($"\nFeature flags:");
Console.WriteLine($"  NewCheckout: {features.NewCheckout}");
Console.WriteLine($"  BetaReports: {features.BetaReports}");

Console.WriteLine("\n--- Where each value came from ---\n");
foreach (var key in new[] { "Database:Host", "Database:MaxPoolSize", "Features:NewCheckout" })
{
    // Walk the providers in reverse: the LAST one holding a value wins.
    var winner = configuration.Providers
        .Reverse()
        .FirstOrDefault(p => p.TryGet(key, out _));

    Console.WriteLine($"  {key,-24} = {configuration[key],-16} (from {winner?.GetType().Name ?? "nothing"})");
}

Console.WriteLine("\nTry these to see precedence in action:");
Console.WriteLine("  DOTNET_ENVIRONMENT=Production dotnet run");
Console.WriteLine("  Database__Host=from-env dotnet run");
Console.WriteLine("  dotnet run -- --Database:MaxPoolSize=200");
Console.WriteLine("  dotnet run -- --Database:MaxPoolSize=999     (fails validation: max is 500)");
Console.WriteLine("\nNote the double underscore in Database__Host: environment variables");
Console.WriteLine("cannot contain ':', so '__' is the separator. Secrets reach a container");
Console.WriteLine("this way -- never in an appsettings file committed to git.");

return 0;
