using AdvancedDI.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdvancedDI.Tests;

/// <summary>
/// Part 3: binding is what turns untyped configuration into a class. These
/// tests use an in-memory source so they do not depend on appsettings.json.
/// </summary>
public class OptionsTests
{
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationName"] = "Test App",
                ["Database:ConnectionString"] = "Server=test;",
                ["Database:MaxRetries"] = "7",
                ["Database:TimeoutSeconds"] = "45",
                ["Email:SmtpServer"] = "smtp.test.local",
                ["Email:Port"] = "2525",
                ["Email:UseSsl"] = "false"
            })
            .Build();

    [Fact]
    public void A_section_binds_onto_a_settings_class()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();
        services.Configure<DatabaseSettings>(
            configuration.GetSection(DatabaseSettings.SectionName));
        using var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

        settings.ConnectionString.Should().Be("Server=test;");
        settings.MaxRetries.Should().Be(7);
        settings.TimeoutSeconds.Should().Be(45);
    }

    [Fact]
    public void Nested_sections_bind_when_the_whole_configuration_is_bound()
    {
        var services = new ServiceCollection();
        services.Configure<AppSettings>(BuildConfiguration());
        using var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;

        settings.ApplicationName.Should().Be("Test App");
        settings.Database.MaxRetries.Should().Be(7);
        settings.Email.Port.Should().Be(2525);
        settings.Email.UseSsl.Should().BeFalse();
    }

    [Fact]
    public void Missing_keys_fall_back_to_the_property_initializer()
    {
        var services = new ServiceCollection();
        services.Configure<EmailSettings>(
            BuildConfiguration().GetSection(EmailSettings.SectionName));
        using var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<EmailSettings>>().Value;

        // FromAddress is absent from the configuration above.
        settings.FromAddress.Should().BeEmpty();
    }

    [Fact]
    public void A_settings_class_can_be_supplied_directly_in_a_test()
    {
        // This is the practical payoff of IOptions over IConfiguration: no
        // configuration system needed to test a consumer.
        var options = Options.Create(new DatabaseSettings { MaxRetries = 99 });

        options.Value.MaxRetries.Should().Be(99);
    }
}
