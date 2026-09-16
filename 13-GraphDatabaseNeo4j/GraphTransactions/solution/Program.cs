using GraphTransactions.Data;
using GraphTransactions.Demos;
using GraphTransactions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var uri = configuration["Neo4j:Uri"] ?? throw new InvalidOperationException("Missing Neo4j:Uri.");
var username = configuration["Neo4j:Username"] ?? throw new InvalidOperationException("Missing Neo4j:Username.");
var password = configuration["Neo4j:Password"] ?? throw new InvalidOperationException("Missing Neo4j:Password.");

await using var driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
await driver.VerifyConnectivityAsync();
Console.WriteLine($"Connected to Neo4j at {uri}");

await GraphSeeder.SeedAsync(driver);
Console.WriteLine("Seed data ensured (Alice, Bob, Acme Corp).");

var part = args.Length > 0 ? args[0] : "all";

if (part is "1" or "all")
{
    Console.WriteLine();
    Console.WriteLine("=== Part 1: Implicit Transactions ===");
    await Part1ImplicitTransactionDemo.RunAsync(driver);
}

if (part is "2" or "all")
{
    Console.WriteLine();
    Console.WriteLine("=== Part 2: Explicit Multi-Statement Transactions ===");
    await Part2ExplicitReferralTransactionDemo.RunAsync(driver);
}

if (part is "3" or "all")
{
    Console.WriteLine();
    Console.WriteLine("=== Part 3: The Cross-Database Contrast with Cosmos DB ===");
    await Part3CrossDatabaseContrastDemo.RunAsync();
}

if (part is "4" or "all")
{
    Console.WriteLine();
    Console.WriteLine("=== Part 4: Concurrent Updates and Locking ===");
    await Part4ConcurrentIncrementDemo.RunAsync(driver);
}

if (part is "5" or "all")
{
    Console.WriteLine();
    Console.WriteLine("=== Part 5: The ReferralService Unit-of-Work Wrapper ===");

    var services = new ServiceCollection();
    services.AddSingleton(driver);
    services.AddSingleton<IReferralService, ReferralService>();
    await using var provider = services.BuildServiceProvider();

    var referralService = provider.GetRequiredService<IReferralService>();

    var newHireId = $"person-{Guid.NewGuid()}";
    var result = await referralService.ReferAsync(GraphSeeder.AliceId, newHireId, GraphSeeder.AcmeId, "Product Designer");
    Console.WriteLine($"Referral succeeded via ReferralService. Alice's referralCount is now {result.ReferrerReferralCount}.");

    try
    {
        await referralService.ReferAsync(
            GraphSeeder.AliceId, $"person-{Guid.NewGuid()}", GraphSeeder.AcmeId, "Analyst",
            simulateFailureBeforeIncrement: true);
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Caught (expected): {ex.Message}");
        Console.WriteLine("The wrapper rolled back all three writes -- callers never see a partial referral.");
    }
}
