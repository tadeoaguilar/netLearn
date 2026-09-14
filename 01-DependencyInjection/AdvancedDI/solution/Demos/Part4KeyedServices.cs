using AdvancedDI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdvancedDI.Demos;

public static class Part4KeyedServices
{
    public static void Run()
    {
        Console.WriteLine("=== PART 4: KEYED SERVICES ===\n");

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddKeyedTransient<INotificationService, EmailNotificationService>("email");
        builder.Services.AddKeyedTransient<INotificationService, SmsNotificationService>("sms");
        builder.Services.AddKeyedTransient<INotificationService, PushNotificationService>("push");

        // The resolver alternative needs the concrete types registered too.
        builder.Services.AddTransient<EmailNotificationService>();
        builder.Services.AddTransient<SmsNotificationService>();
        builder.Services.AddTransient<PushNotificationService>();
        builder.Services.AddSingleton<NotificationServiceResolver>();

        var host = builder.Build();

        Console.WriteLine("Resolved by key:");
        host.Services.GetRequiredKeyedService<INotificationService>("email")
            .Send("Welcome to our platform!");
        host.Services.GetRequiredKeyedService<INotificationService>("sms")
            .Send("Your code is 1234");
        host.Services.GetRequiredKeyedService<INotificationService>("push")
            .Send("New message received");

        Console.WriteLine("\nResolved through the resolver (pre-.NET 8 style):");
        var resolver = host.Services.GetRequiredService<NotificationServiceResolver>();
        resolver.GetService("email").Send("Same result, more plumbing");
    }
}
