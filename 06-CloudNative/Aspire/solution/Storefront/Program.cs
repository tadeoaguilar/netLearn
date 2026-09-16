var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// "http://catalogapi" is resolved by Aspire's service discovery (wired in
// via ConfigureHttpClientDefaults in ServiceDefaults) to wherever the
// AppHost actually put CatalogApi -- there is no hardcoded port here, and
// with .WithReplicas(2) on CatalogApi (see AppHost/Program.cs) it also
// load-balances across both instances. See EXERCISE.md Part A.4.
builder.Services.AddHttpClient<CatalogApiClient>(client =>
{
    client.BaseAddress = new Uri("http://catalogapi");
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", async (CatalogApiClient catalog) =>
{
    var products = await catalog.GetProductsAsync();
    return Results.Ok(new { message = "Welcome to the storefront", products });
});

app.Run();

public partial class Program;

public class CatalogApiClient(HttpClient httpClient)
{
    public async Task<List<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<List<ProductDto>>("/products", cancellationToken) ?? [];
}

public record ProductDto(int Id, string Name, decimal Price);
