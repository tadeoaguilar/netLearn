using EfCoreQuerying.Demos;
using EfCoreQuerying.Persistence;
using EfCoreQuerying.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connectionString = configuration.GetConnectionString("Default")
                        ?? throw new InvalidOperationException("Missing ConnectionStrings:Default in appsettings.json");

var optionsBuilder = new DbContextOptionsBuilder<LibraryDbContext>()
    .UseNpgsql(connectionString);

await using var context = new LibraryDbContext(optionsBuilder.Options);

Console.WriteLine("Ensuring database exists and schema is up to date...");
await context.Database.EnsureCreatedAsync();

Console.WriteLine("Seeding data (no-op if already seeded)...");
await LibrarySeeder.SeedAsync(context);

var parts = new Dictionary<string, Func<LibraryDbContext, Task>>
{
    ["1"] = Part1BasicLinq.RunAsync,
    ["2"] = Part2Projections.RunAsync,
    ["3"] = Part3Includes.RunAsync,
    ["4"] = Part4SplitQuery.RunAsync,
    ["5"] = Part5GroupBy.RunAsync,
    ["6"] = Part6ClientEval.RunAsync,
    ["7"] = Part7RawSql.RunAsync,
    ["8"] = Part8PostgresFeatures.RunAsync,
    ["9"] = Part9CompiledQuery.RunAsync,
};

var selection = args.Length > 0 ? args[0] : "all";

if (selection == "all")
{
    foreach (var (_, run) in parts.OrderBy(p => int.Parse(p.Key)))
    {
        await run(context);
    }
}
else if (parts.TryGetValue(selection, out var runOne))
{
    await runOne(context);
}
else
{
    Console.WriteLine($"Unknown part '{selection}'. Usage: dotnet run -- [1-9|all]");
}
