using FunctionApi.KeyVault;
using FunctionApi.Notes;
using FunctionApi.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// The classic isolated-worker host, not the newer ASP.NET Core integration
// (Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore). That
// integration lets you reuse ASP.NET Core's app.UseAuthentication()/
// [Authorize] pipeline, but as of this writing that pipeline does not wire
// up cleanly for Functions without extra glue -- see EXERCISE.md Part 2.2
// for why this module hand-rolls EntraIdAuthenticationMiddleware as an
// IFunctionsWorkerMiddleware instead, on the plain HttpRequestData/
// HttpResponseData model.
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<EntraIdAuthenticationMiddleware>();
    })
    .ConfigureServices(services =>
    {
        services
            .AddOptions<EntraIdOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                configuration.GetSection("EntraId").Bind(options));

        services
            .AddOptions<KeyVaultOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                configuration.GetSection("KeyVault").Bind(options));

        services.AddSingleton<IEntraIdConfigurationProvider, EntraIdConfigurationProvider>();
        services.AddSingleton<ITokenValidator, EntraIdTokenValidator>();
        services.AddSingleton<ISecretReader, KeyVaultSecretReader>();

        // Singleton: state needs to survive across invocations on the same
        // instance. See InMemoryNotesStore's comment on what that does and
        // doesn't guarantee on a Consumption plan.
        services.AddSingleton<INotesStore, InMemoryNotesStore>();

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run();
