using System.Collections.Generic;
using EfCoreLoggingAndHealthChecks.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EfCoreLoggingAndHealthChecks.Tests.Infrastructure;

/// <summary>
/// Hosts the real minimal API from <c>solution/Program.cs</c> with the
/// "ConnectionStrings:Default" value overridden at the configuration level --
/// exactly what a real deployment does with an environment variable, so this
/// exercises the actual startup path rather than a test-only substitute.
/// </summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TestApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString
            });
        });
    }

    /// <summary>Applies the InitialCreate migration -- an explicit deploy step,
    /// same as Program.cs's comment describes for a real environment.</summary>
    public async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
