using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BasicDI.WithDI;

Console.WriteLine("\n=== WITH DEPENDENCY INJECTION (DI Container) ===\n");

// Create and configure the DI container
var builder = Host.CreateApplicationBuilder(args);

// Register services (tell the container what to inject)
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<IUserRepository, UserRepository>();
builder.Services.AddTransient<ILogger, ConsoleLogger>();
builder.Services.AddTransient<UserService>();

// Try different lifetimes: Transient, Scoped, Singleton
// Uncomment one at a time to see the difference:

// TRANSIENT: New instance every time
//builder.Services.AddTransient<IRequestIdGenerator, RequestIdGenerator>();

// SINGLETON: Single instance for application lifetime
 builder.Services.AddSingleton<IRequestIdGenerator, RequestIdGenerator>();


// Build the service provider
var host = builder.Build();

// Resolve UserService (container automatically injects dependencies)
var userService = host.Services.GetRequiredService<UserService>();
userService.RegisterUser("alice_wonder", "alice@example.com");

Console.WriteLine("\nDI Container handled all the wiring automatically!");



Console.WriteLine("\n=== TESTING SERVICE LIFETIMES ===\n");






Console.WriteLine("Resolving service 3 times:");
var gen1 = host.Services.GetRequiredService<IRequestIdGenerator>();
var gen2 = host.Services.GetRequiredService<IRequestIdGenerator>();
var gen3 = host.Services.GetRequiredService<IRequestIdGenerator>();

Console.WriteLine($"\ngen1 ID: {gen1.GetRequestId()}");
Console.WriteLine($"gen2 ID: {gen2.GetRequestId()}");
Console.WriteLine($"gen3 ID: {gen3.GetRequestId()}");

Console.WriteLine($"\nAre they the same? {gen1.GetRequestId() == gen2.GetRequestId()}");