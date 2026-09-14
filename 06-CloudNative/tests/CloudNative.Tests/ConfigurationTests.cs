using CloudNative.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CloudNative.Tests;

public class ConfigurationTests
{
    private static IConfiguration Build(params (string Source, Dictionary<string, string?> Values)[] layers)
    {
        var builder = new ConfigurationBuilder();
        foreach (var layer in layers) builder.AddInMemoryCollection(layer.Values);
        return builder.Build();
    }

    [Fact]
    public void A_later_source_overrides_an_earlier_one()
    {
        // The whole mechanism of 12-factor configuration in one assertion.
        var configuration = Build(
            ("appsettings.json", new() { ["Database:Host"] = "localhost", ["Database:Port"] = "5432" }),
            ("appsettings.Production.json", new() { ["Database:Host"] = "prod-db" }),
            ("environment", new() { ["Database:Host"] = "from-env" }));

        configuration["Database:Host"].Should().Be("from-env");
        configuration["Database:Port"].Should().Be("5432", "nothing overrode it");
    }

    [Fact]
    public void Valid_settings_bind_and_pass_validation()
    {
        var services = new ServiceCollection();
        services.AddOptions<DatabaseSettings>()
            .Bind(Build(("x", new()
            {
                ["Database:Host"] = "db",
                ["Database:Port"] = "5432",
                ["Database:Name"] = "billing",
                ["Database:MaxPoolSize"] = "20"
            })).GetSection(DatabaseSettings.SectionName))
            .ValidateDataAnnotations();

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

        settings.Host.Should().Be("db");
        settings.MaxPoolSize.Should().Be(20);
    }

    [Theory]
    [InlineData("", "5432", "billing", "20")]          // missing host
    [InlineData("db", "0", "billing", "20")]           // port out of range
    [InlineData("db", "5432", "", "20")]               // missing name
    [InlineData("db", "5432", "billing", "99999")]     // pool size out of range
    public void Invalid_settings_fail_fast_rather_than_at_first_use(
        string host, string port, string name, string poolSize)
    {
        var services = new ServiceCollection();
        services.AddOptions<DatabaseSettings>()
            .Bind(Build(("x", new()
            {
                ["Database:Host"] = host,
                ["Database:Port"] = port,
                ["Database:Name"] = name,
                ["Database:MaxPoolSize"] = poolSize
            })).GetSection(DatabaseSettings.SectionName))
            .ValidateDataAnnotations();

        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void A_connection_string_never_includes_the_password()
    {
        var settings = new DatabaseSettings
        {
            Host = "db", Port = 5432, Name = "billing", MaxPoolSize = 10, Password = "hunter2"
        };

        settings.ToConnectionString().Should().NotContain("hunter2").And.Contain("***");
    }
}
