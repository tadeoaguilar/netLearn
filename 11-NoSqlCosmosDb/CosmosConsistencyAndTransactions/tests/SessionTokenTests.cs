using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Tests;

[Collection("Cosmos emulator")]
public class SessionTokenTests(CosmosFixture fixture)
{
    [Fact]
    public async Task Write_response_includes_a_non_empty_session_token()
    {
        var customer = new Customer { Name = "Session Token Test", Email = "st@example.com" };
        var response = await fixture.Customers.CreateItemAsync(customer, new PartitionKey(customer.Id));

        response.Headers.Session.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Second_client_reads_the_written_value_when_the_session_token_is_propagated()
    {
        var customer = new Customer { Name = "Propagated", Email = "prop@example.com", LoyaltyPoints = 99 };
        var writeResponse = await fixture.Customers.CreateItemAsync(customer, new PartitionKey(customer.Id));

        using var otherClient = new CosmosClient(fixture.ConnectionString, new CosmosClientOptions { ConsistencyLevel = ConsistencyLevel.Session });
        var otherContainer = otherClient.GetContainer(fixture.DatabaseName, fixture.CustomersContainerName);

        var readOptions = new ItemRequestOptions { SessionToken = writeResponse.Headers.Session };
        var readResponse = await otherContainer.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id), readOptions);

        readResponse.Resource.LoyaltyPoints.Should().Be(99);
    }

    [Fact]
    public async Task Second_client_without_a_propagated_token_still_reads_correctly_on_the_single_node_emulator()
    {
        // On a real multi-region account, Session consistency guarantees
        // read-your-writes ONLY when the session token is propagated -- see
        // the test above. The emulator is a single node with no replicas,
        // so a missing token has nothing to expose here; this test
        // documents that limitation rather than pretending to prove the
        // distributed guarantee.
        var customer = new Customer { Name = "No Token", Email = "notoken@example.com", LoyaltyPoints = 13 };
        await fixture.Customers.CreateItemAsync(customer, new PartitionKey(customer.Id));

        using var otherClient = new CosmosClient(fixture.ConnectionString, new CosmosClientOptions { ConsistencyLevel = ConsistencyLevel.Session });
        var otherContainer = otherClient.GetContainer(fixture.DatabaseName, fixture.CustomersContainerName);

        var readResponse = await otherContainer.ReadItemAsync<Customer>(customer.Id, new PartitionKey(customer.Id));

        readResponse.Resource.LoyaltyPoints.Should().Be(13);
    }
}
