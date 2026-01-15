using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DILifetimes.Services;

Console.WriteLine("=== TRANSIENT LIFETIME ===\n");

var builder = Host.CreateApplicationBuilder(args);

// Register as Transient - NEW instance every time
builder.Services.AddTransient<ITransientService, TransientService>();
builder.Services.AddSingleton<ISingletonService, SingletonService>();
builder.Services.AddScoped<IScopedService, ScopedService>();
builder.Services.AddTransient<ScopedDemo>();
var host = builder.Build();

// Request the service 3 times
Console.WriteLine("Requesting Transient service 3 times:");
var service1 = host.Services.GetRequiredService<ITransientService>();
var service2 = host.Services.GetRequiredService<ITransientService>();
var service3 = host.Services.GetRequiredService<ITransientService>();
Console.WriteLine("Requesting Singleton service 3 times:");
var serviceS1 = host.Services.GetRequiredService<ISingletonService>();
var serviceS2 = host.Services.GetRequiredService<ISingletonService>();
var serviceS3 = host.Services.GetRequiredService<ISingletonService>();



Console.WriteLine("=== TRANSIENT LIFETIME ===\n");
Console.WriteLine($"\nService 1 ID: {service1.InstanceId}");
Console.WriteLine($"Service 2 ID: {service2.InstanceId}");
Console.WriteLine($"Service 3 ID: {service3.InstanceId}");
service1.DoWork();
service2.DoWork();
service3.DoWork();
Console.WriteLine($"\nAre they the same instance? {service1.InstanceId == service2.InstanceId && service2.InstanceId == service3.InstanceId}");

Console.WriteLine("=== SINGLETON LIFETIME ===\n");
Console.WriteLine($"\nService S1 ID: {serviceS1.InstanceId}");
Console.WriteLine($"Service S2 ID: {serviceS2.InstanceId}");
Console.WriteLine($"Service S3 ID: {serviceS3.InstanceId}");
serviceS1.DoWork();
serviceS2.DoWork();
serviceS3.DoWork();
Console.WriteLine($"\nAre they the same instance? {serviceS1.InstanceId == serviceS2.InstanceId && serviceS2.InstanceId == serviceS3.InstanceId}");


Console.WriteLine("=== SCOPED LIFETIME ===\n");
// Create Scope 1
Console.WriteLine("--- Scope 1 ---");
using (var scope1 = host.Services.CreateScope())
{
    var demo1 = scope1.ServiceProvider.GetRequiredService<ScopedDemo>();
    demo1.ShowIds();

    // Request again within same scope
    var demo2 = scope1.ServiceProvider.GetRequiredService<ScopedDemo>();
    demo2.ShowIds();
}

Console.WriteLine();

// Create Scope 2
Console.WriteLine("--- Scope 2 ---");
using (var scope2 = host.Services.CreateScope())
{
    var demo3 = scope2.ServiceProvider.GetRequiredService<ScopedDemo>();
    demo3.ShowIds();
}
