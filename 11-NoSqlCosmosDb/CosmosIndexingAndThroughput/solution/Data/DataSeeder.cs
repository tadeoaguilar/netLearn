using CosmosIndexingAndThroughput.Models;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Data;

// Seeds a modest but non-trivial dataset (20 customers, 120+ orders) so that
// RU-cost differences between query and indexing strategies are actually
// observable, instead of every query costing ~1 RU on a handful of rows.
public static class DataSeeder
{
    private static readonly string[] Cities = { "Seattle", "Austin", "Denver", "Miami", "Boston", "Chicago", "Phoenix" };
    private static readonly string[] Tiers = { "Standard", "Silver", "Gold" };
    private static readonly string[] Statuses = { "Pending", "Shipped", "Delivered", "Cancelled" };

    private static readonly (string Id, string Name, decimal Price)[] Catalog =
    {
        ("P001", "Wireless Mouse", 24.99m),
        ("P002", "Mechanical Keyboard", 89.99m),
        ("P003", "USB-C Hub", 34.50m),
        ("P004", "27-inch Monitor", 249.00m),
        ("P005", "Laptop Stand", 39.95m),
        ("P006", "Webcam 1080p", 54.00m),
        ("P007", "Noise Cancelling Headphones", 199.00m),
        ("P008", "Desk Lamp", 29.99m),
    };

    public static async Task<IReadOnlyList<Customer>> SeedCustomersAsync(Container customers, int count = 20)
    {
        var existingCount = await CountItemsAsync(customers);
        if (existingCount >= count)
        {
            return await LoadAllAsync(customers);
        }

        var random = new Random(42);
        var result = new List<Customer>();
        for (var i = 1; i <= count; i++)
        {
            var customer = new Customer
            {
                Id = $"customer-{i:D3}",
                Name = $"Customer {i}",
                Email = $"customer{i}@example.com",
                City = Cities[random.Next(Cities.Length)],
                Tier = Tiers[random.Next(Tiers.Length)],
            };
            await customers.UpsertItemAsync(customer, new PartitionKey(customer.Id));
            result.Add(customer);
        }

        return result;
    }

    public static async Task SeedOrdersAsync(Container orders, IReadOnlyList<Customer> customers, int count = 120)
    {
        var existingCount = await CountItemsAsync(orders);
        if (existingCount >= count)
        {
            return;
        }

        var random = new Random(99);
        var startDate = DateTimeOffset.UtcNow.AddDays(-90);

        for (var i = 1; i <= count; i++)
        {
            var customer = customers[random.Next(customers.Count)];
            var lineCount = random.Next(1, 5);
            var lines = new List<OrderLine>();
            for (var l = 0; l < lineCount; l++)
            {
                var product = Catalog[random.Next(Catalog.Length)];
                var quantity = random.Next(1, 4);
                lines.Add(new OrderLine
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = quantity,
                    UnitPrice = product.Price,
                });
            }

            var order = new Order
            {
                Id = $"order-{i:D4}",
                CustomerId = customer.Id,
                OrderDate = startDate.AddMinutes(random.Next(0, 90 * 24 * 60)),
                Status = Statuses[random.Next(Statuses.Length)],
                OrderLines = lines,
                TotalAmount = lines.Sum(l => l.Quantity * l.UnitPrice),
            };

            await orders.UpsertItemAsync(order, new PartitionKey(order.CustomerId));
        }
    }

    private static async Task<List<Customer>> LoadAllAsync(Container customers)
    {
        var result = new List<Customer>();
        using var iterator = customers.GetItemQueryIterator<Customer>(new QueryDefinition("SELECT * FROM c"));
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            result.AddRange(page);
        }

        return result;
    }

    private static async Task<int> CountItemsAsync(Container container)
    {
        using var iterator = container.GetItemQueryIterator<int>(new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));
        var response = await iterator.ReadNextAsync();
        return response.FirstOrDefault();
    }
}
