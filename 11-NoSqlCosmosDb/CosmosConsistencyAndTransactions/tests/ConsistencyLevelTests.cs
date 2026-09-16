using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Tests;

[Collection("Cosmos emulator")]
public class ConsistencyLevelTests(CosmosFixture fixture)
{
    [Fact]
    public void Client_reports_the_configured_consistency_level()
    {
        fixture.Client.ClientOptions.ConsistencyLevel.Should().Be(ConsistencyLevel.Session);
    }

    [Fact]
    public async Task Account_reports_a_default_consistency_level()
    {
        var account = await fixture.Client.ReadAccountAsync();

        account.Consistency.Should().NotBeNull();
        Enum.IsDefined(typeof(ConsistencyLevel), account.Consistency.DefaultConsistencyLevel).Should().BeTrue();
    }

    [Fact]
    public async Task Per_request_ConsistencyLevel_override_still_returns_the_written_value()
    {
        var customer = new Customer { Name = "Test Eventual Read", Email = "eventual@example.com", LoyaltyPoints = 42 };
        await fixture.Customers.CreateItemAsync(customer, new PartitionKey(customer.Id));

        var readOptions = new ItemRequestOptions { ConsistencyLevel = ConsistencyLevel.Eventual };
        var response = await fixture.Customers.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id), readOptions);

        response.Resource.LoyaltyPoints.Should().Be(42);
    }

    [Fact]
    public async Task A_client_configured_with_a_weaker_level_can_still_read_and_write()
    {
        using var eventualClient = new CosmosClient(fixture.ConnectionString, new CosmosClientOptions { ConsistencyLevel = ConsistencyLevel.Eventual });
        var container = eventualClient.GetContainer(fixture.DatabaseName, fixture.CustomersContainerName);

        var customer = new Customer { Name = "Weaker Client", Email = "weaker@example.com", LoyaltyPoints = 7 };
        await container.CreateItemAsync(customer, new PartitionKey(customer.Id));
        var readBack = await container.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));

        readBack.Resource.LoyaltyPoints.Should().Be(7);
    }
}
