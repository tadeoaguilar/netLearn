using CosmosIndexingAndThroughput.Models;

namespace CosmosIndexingAndThroughput.Data;

// A single, deterministic order shape used by the walkthroughs so that
// write-cost comparisons (default policy vs. custom policy, RU logging,
// retry demo) are all measuring the same document shape.
public static class SampleOrderFactory
{
    public const string DemoProductId = "P099";

    public static Order Create(string customerId, string idSuffix)
    {
        return new Order
        {
            Id = $"sample-{idSuffix}",
            CustomerId = customerId,
            OrderDate = DateTimeOffset.UtcNow,
            Status = "Pending",
            OrderLines = new List<OrderLine>
            {
                new()
                {
                    ProductId = DemoProductId,
                    ProductName = "Indexing Demo Widget",
                    Quantity = 2,
                    UnitPrice = 19.99m,
                },
            },
            TotalAmount = 39.98m,
        };
    }
}
