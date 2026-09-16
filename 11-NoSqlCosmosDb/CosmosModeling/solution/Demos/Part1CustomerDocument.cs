using CosmosModeling.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosModeling.Demos;

/// <summary>Part 1: the Customer document and choosing /id as its partition key.</summary>
public static class Part1CustomerDocument
{
    public static async Task RunAsync(Container customers)
    {
        Console.WriteLine("=== Part 1: The Customer document (partition key /id) ===\n");

        var customer = new Customer
        {
            Id = $"cust-{Guid.NewGuid():N}",
            Name = "Ada Lovelace",
            Email = "ada@example.com",
        };

        // Because the partition key IS the item's own id, Cosmos can route this write to
        // exactly one physical partition without touching any other -- the cheapest possible
        // write in RU terms, and it stays that cheap no matter how many customers exist.
        await customers.UpsertItemAsync(customer, new PartitionKey(customer.Id));

        var response = await customers.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));
        Console.WriteLine($"Point-read customer '{response.Resource.Name}' <{response.Resource.Email}> for {response.RequestCharge} RU.");
        Console.WriteLine();
    }
}
