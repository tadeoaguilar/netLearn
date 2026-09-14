using CloudNative.Microservices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<CatalogueClient>();
builder.Services.AddSingleton<InventoryClient>();
builder.Services.AddSingleton<ProductComposer>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "Product gateway",
    note = "Composes a catalogue service and an inventory service.",
    endpoints = new[]
    {
        "GET  /products",
        "GET  /products/{sku}",
        "POST /inventory/toggle   (simulate the inventory service failing)",
        "POST /inventory/slow     (simulate it becoming slow)"
    }
}));

app.MapGet("/products", async (ProductComposer composer, CancellationToken cancellationToken)
    => Results.Ok(await composer.ListAsync(cancellationToken)));

app.MapGet("/products/{sku}", async (
    string sku, ProductComposer composer, CancellationToken cancellationToken) =>
{
    var product = await composer.GetAsync(sku, cancellationToken);
    return product is null ? Results.NotFound() : Results.Ok(product);
});

app.MapPost("/inventory/toggle", (InventoryClient inventory) =>
{
    inventory.IsDown = !inventory.IsDown;
    return Results.Ok(new { inventoryDown = inventory.IsDown });
});

app.MapPost("/inventory/slow", (InventoryClient inventory) =>
{
    inventory.Latency = inventory.Latency > TimeSpan.FromSeconds(1)
        ? TimeSpan.FromMilliseconds(20)
        : TimeSpan.FromSeconds(3);

    return Results.Ok(new { latencyMs = inventory.Latency.TotalMilliseconds });
});

app.Run();

public partial class Program;
