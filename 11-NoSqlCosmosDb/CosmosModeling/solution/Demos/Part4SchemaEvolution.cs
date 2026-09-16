using System.Text;
using CosmosModeling.Domain;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

namespace CosmosModeling.Demos;

/// <summary>Part 4: schema evolution without migrations -- old and new document shapes coexist.</summary>
public static class Part4SchemaEvolution
{
    public static async Task RunAsync(Container orders)
    {
        Console.WriteLine("=== Part 4: Schema evolution without migrations ===\n");

        var customerId = $"cust-{Guid.NewGuid():N}";
        var oldOrderId = Guid.NewGuid().ToString();

        // Simulate a document written by OLD code, before SchemaVersion and ShippingAddress
        // existed. Written as raw JSON (not via the Order class) to prove this is a document
        // that genuinely never had those properties on the wire -- not just a C# null.
        var oldShapeDocument = new JObject
        {
            ["id"] = oldOrderId,
            ["customerId"] = customerId,
            ["OrderDate"] = DateTimeOffset.UtcNow.AddMonths(-6).ToString("O"),
            ["Status"] = "Delivered",
            ["OrderLines"] = new JArray
            {
                new JObject { ["ProductId"] = "sku-1", ["ProductName"] = "Keyboard", ["Quantity"] = 1, ["UnitPrice"] = 79.99m },
            },
            // Deliberately absent: SchemaVersion, ShippingAddress -- the "before" shape.
        };

        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(oldShapeDocument.ToString())))
        {
            using var createResponse = await orders.CreateItemStreamAsync(stream, new PartitionKey(customerId));
            createResponse.EnsureSuccessStatusCode();
        }

        // Read it back through TODAY's Order type, which has both fields. No migration ran --
        // there is no schema in Cosmos to migrate. Newtonsoft simply leaves the missing
        // properties at their C# default: 0 for SchemaVersion, null for ShippingAddress.
        var oldOrder = await orders.ReadItemAsync<Order>(oldOrderId, new PartitionKey(customerId));
        Console.WriteLine(
            $"Old-shape order deserialized without error. SchemaVersion={oldOrder.Resource.SchemaVersion} " +
            $"(default -- the field never existed), ShippingAddress={(oldOrder.Resource.ShippingAddress is null ? "null" : "set")}.");

        // Reader code MUST handle both shapes deliberately -- here, by treating a missing
        // ShippingAddress as "no shipping info on file" instead of crashing on a null ref.
        var shippingLine = oldOrder.Resource.ShippingAddress is { } address
            ? $"{address.Line1}, {address.City}"
            : "(no shipping address on file -- this order pre-dates that field)";
        Console.WriteLine($"Shipping: {shippingLine}");

        // A NEW order, written by today's code, has both fields populated from the start.
        var newOrder = new Order
        {
            CustomerId = customerId,
            Status = "Placed",
            OrderLines = [new OrderLine { ProductId = "sku-2", ProductName = "Mouse", Quantity = 1, UnitPrice = 24.99m }],
            ShippingAddress = new ShippingAddress { Line1 = "1 Infinite Loop", City = "Cupertino", PostalCode = "95014", Country = "US" },
        };
        await orders.UpsertItemAsync(newOrder, new PartitionKey(customerId));
        Console.WriteLine($"New order written with SchemaVersion={newOrder.SchemaVersion} and a ShippingAddress.");
        Console.WriteLine();

        Console.WriteLine("SchemaVersion convention: bump Order.SchemaVersion whenever a change to the shape");
        Console.WriteLine("is significant enough that reader code needs to branch on it deliberately -- e.g.");
        Console.WriteLine("'if SchemaVersion < 2, this order predates ShippingAddress; go look it up another");
        Console.WriteLine("way' -- rather than guessing purely from which fields happen to be present.");
        Console.WriteLine();
    }
}
