using CosmosQuerying.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosQuerying.Seed;

/// <summary>
/// Deterministic seed data: 25 customers and 180 orders (1-4 line items
/// each), spread across four statuses and roughly 20 months of order dates.
/// Deterministic because it's driven by a fixed <see cref="Random"/> seed,
/// so every run -- and every test run against the same emulator instance --
/// produces exactly the same documents, which is what lets EXERCISE.md and
/// the tests assert on concrete facts ("every 'Pending' order really has
/// status Pending") instead of "some number greater than zero".
/// </summary>
public static class OrderSeeder
{
    private static readonly string[] FirstNames =
    [
        "Ava", "Liam", "Noah", "Emma", "Olivia", "Mason", "Sophia", "Lucas",
        "Isabella", "Ethan", "Mia", "James", "Amelia", "Benjamin", "Harper",
        "Elijah", "Evelyn", "Logan", "Abigail", "Alexander", "Emily", "Jack",
        "Charlotte", "Henry", "Grace",
    ];

    private static readonly string[] LastNames =
    [
        "Nguyen", "Garcia", "Patel", "Kowalski", "Andersen", "Osei", "Rossi",
        "Dubois", "Kim", "Silva",
    ];

    private static readonly string[] Cities =
    [
        "Seattle", "Austin", "Denver", "Chicago", "Miami", "Boston",
        "Portland", "Phoenix", "Atlanta", "Minneapolis",
    ];

    private static readonly string[] Tiers = ["Standard", "Silver", "Gold"];

    private static readonly string[] ProductNames =
    [
        "Wireless Mouse", "Mechanical Keyboard", "USB-C Hub", "Laptop Stand",
        "Noise-Cancelling Headphones", "Webcam", "Monitor Arm", "Desk Lamp",
        "Portable SSD", "Bluetooth Speaker", "Ergonomic Chair Cushion",
        "Cable Organizer", "Standing Desk Mat", "Ring Light", "Phone Stand",
    ];

    private static readonly string[] Statuses = ["Pending", "Shipped", "Delivered", "Cancelled"];

    public static async Task SeedAsync(Container customers, Container orders, CancellationToken cancellationToken = default)
    {
        using var countIterator = customers.GetItemQueryIterator<int>(new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));
        if (countIterator.HasMoreResults)
        {
            var countPage = await countIterator.ReadNextAsync(cancellationToken);
            if (countPage.FirstOrDefault() > 0)
            {
                return; // Already seeded -- keeps this idempotent across re-runs.
            }
        }

        var random = new Random(20260915); // Fixed seed: same data every run.

        const int customerCount = 25;
        var customerList = new List<Customer>(customerCount);
        for (var i = 0; i < customerCount; i++)
        {
            var firstName = FirstNames[random.Next(FirstNames.Length)];
            var lastName = LastNames[random.Next(LastNames.Length)];
            var customer = new Customer
            {
                Id = $"cust-{i + 1:D3}",
                Name = $"{firstName} {lastName}",
                Email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}{i}@example.com",
                City = Cities[random.Next(Cities.Length)],
                Tier = Tiers[random.Next(Tiers.Length)],
            };
            customerList.Add(customer);
        }

        foreach (var customer in customerList)
        {
            await customers.CreateItemAsync(customer, new PartitionKey(customer.Id), cancellationToken: cancellationToken);
        }

        const int orderCount = 180;
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < orderCount; i++)
        {
            var customer = customerList[random.Next(customerList.Count)];

            var lineCount = random.Next(1, 5); // 1-4 line items
            var lines = new List<OrderLine>(lineCount);
            for (var l = 0; l < lineCount; l++)
            {
                var quantity = random.Next(1, 6);
                var unitPrice = Math.Round((decimal)(random.NextDouble() * 180 + 10), 2);
                lines.Add(new OrderLine
                {
                    ProductName = ProductNames[random.Next(ProductNames.Length)],
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    LineTotal = Math.Round(quantity * unitPrice, 2),
                });
            }

            var order = new Order
            {
                Id = $"order-{i + 1:D4}",
                CustomerId = customer.Id,
                OrderDate = startDate.AddDays(random.Next(0, 600)).AddHours(random.Next(0, 24)),
                Status = Statuses[random.Next(Statuses.Length)],
                Lines = lines,
                TotalAmount = Math.Round(lines.Sum(l => l.LineTotal), 2),
            };

            await orders.CreateItemAsync(order, new PartitionKey(order.CustomerId), cancellationToken: cancellationToken);
        }
    }
}
