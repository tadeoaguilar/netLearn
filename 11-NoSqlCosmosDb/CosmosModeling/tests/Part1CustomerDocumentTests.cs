using CosmosModeling.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosModeling.Tests;

[Collection(CosmosModelingCollection.Name)]
public class Part1CustomerDocumentTests(CosmosModelingFixture fixture)
{
    [Fact]
    public async Task Customers_Container_Has_PartitionKeyPath_Id()
    {
        var response = await fixture.CustomersContainer.ReadContainerAsync();

        response.Resource.PartitionKeyPath.Should().Be("/id");
    }

    [Fact]
    public async Task Customer_RoundTrips_Through_Upsert_And_Point_Read()
    {
        var customer = new Customer
        {
            Id = $"cust-{Guid.NewGuid():N}",
            Name = "Grace Hopper",
            Email = "grace@example.com",
        };

        await fixture.CustomersContainer.UpsertItemAsync(customer, new PartitionKey(customer.Id));

        var read = await fixture.CustomersContainer.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));

        read.Resource.Id.Should().Be(customer.Id);
        read.Resource.Name.Should().Be("Grace Hopper");
        read.Resource.Email.Should().Be("grace@example.com");
    }

    [Fact]
    public async Task Customer_Id_Serializes_To_Lowercase_Id_Property()
    {
        // The container's partition key path ("/id") only matches the JSON on the wire, not
        // the CLR property name -- this proves the [JsonProperty("id")] mapping actually
        // works, not just that a lookup by C# Id happens to succeed.
        var customer = new Customer { Id = $"cust-{Guid.NewGuid():N}", Name = "Alan Turing", Email = "alan@example.com" };
        await fixture.CustomersContainer.UpsertItemAsync(customer, new PartitionKey(customer.Id));

        using var response = await fixture.CustomersContainer.ReadItemStreamAsync(customer.Id, new PartitionKey(customer.Id));
        using var reader = new StreamReader(response.Content);
        var json = await reader.ReadToEndAsync();

        json.Should().Contain($"\"id\":\"{customer.Id}\"");
    }
}
