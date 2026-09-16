using CosmosIndexingAndThroughput.Data;
using CosmosIndexingAndThroughput.Metrics;
using CosmosIndexingAndThroughput.Models;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Tests;

[Collection(nameof(CosmosCollection))]
public class RequestChargeTests : IAsyncLifetime
{
    private const string ContainerName = "orders-request-charge-tests";

    private readonly CosmosFixture _fixture;
    private Container _orders = null!;

    public RequestChargeTests(CosmosFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var response = await _fixture.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ContainerName, "/customerId"));
        _orders = response.Container;
    }

    public async Task DisposeAsync()
    {
        await _orders.DeleteContainerAsync();
    }

    [Fact]
    public async Task Write_reports_a_positive_request_charge()
    {
        var order = SampleOrderFactory.Create("customer-write", Guid.NewGuid().ToString());

        var response = await _orders.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        response.RequestCharge.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Point_read_reports_a_positive_request_charge()
    {
        var order = SampleOrderFactory.Create("customer-point-read", Guid.NewGuid().ToString());
        await _orders.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

        var response = await _orders.ReadItemAsync<Order>(order.Id, new PartitionKey(order.CustomerId));

        response.RequestCharge.Should().BeGreaterThan(0);
        response.Resource.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task Single_partition_query_reports_a_positive_request_charge()
    {
        var customerId = "customer-single-partition";
        await _orders.UpsertItemAsync(SampleOrderFactory.Create(customerId, Guid.NewGuid().ToString()), new PartitionKey(customerId));

        var (results, charge) = await _orders.ExecuteAndMeasureAsync<Order>(
            new QueryDefinition("SELECT * FROM o WHERE o.customerId = @cid").WithParameter("@cid", customerId),
            new QueryRequestOptions { PartitionKey = new PartitionKey(customerId) });

        charge.Should().BeGreaterThan(0);
        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Cross_partition_query_reports_a_positive_request_charge()
    {
        await _orders.UpsertItemAsync(SampleOrderFactory.Create("customer-cross-1", Guid.NewGuid().ToString()), new PartitionKey("customer-cross-1"));
        await _orders.UpsertItemAsync(SampleOrderFactory.Create("customer-cross-2", Guid.NewGuid().ToString()), new PartitionKey("customer-cross-2"));

        var (results, charge) = await _orders.ExecuteAndMeasureAsync<Order>(
            new QueryDefinition("SELECT * FROM o WHERE o.status = @status").WithParameter("@status", "Pending"));

        charge.Should().BeGreaterThan(0);
        results.Should().NotBeEmpty();
    }
}
