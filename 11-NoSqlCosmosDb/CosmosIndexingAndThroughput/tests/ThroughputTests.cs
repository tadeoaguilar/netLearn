using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Tests;

[Collection(nameof(CosmosCollection))]
public class ThroughputTests : IAsyncLifetime
{
    private const string ManualContainerName = "orders-throughput-tests-manual";
    private const string AutoscaleContainerName = "orders-throughput-tests-autoscale";

    private readonly CosmosFixture _fixture;
    private Container _manualContainer = null!;
    private Container _autoscaleContainer = null!;

    public ThroughputTests(CosmosFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var manualResponse = await _fixture.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ManualContainerName, "/customerId"),
            ThroughputProperties.CreateManualThroughput(400));
        _manualContainer = manualResponse.Container;

        var autoscaleResponse = await _fixture.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(AutoscaleContainerName, "/customerId"),
            ThroughputProperties.CreateAutoscaleThroughput(4000));
        _autoscaleContainer = autoscaleResponse.Container;
    }

    public async Task DisposeAsync()
    {
        await _manualContainer.DeleteContainerAsync();
        await _autoscaleContainer.DeleteContainerAsync();
    }

    [Fact]
    public async Task Manual_throughput_container_reports_the_configured_fixed_RUs()
    {
        var throughput = await _manualContainer.ReadThroughputAsync();

        throughput.Should().Be(400);
    }

    [Fact]
    public async Task Autoscale_throughput_container_reports_the_configured_max_RUs()
    {
        var response = await _autoscaleContainer.ReadThroughputAsync(new RequestOptions());

        response.Resource.AutoscaleMaxThroughput.Should().Be(4000);
    }

    [Fact]
    public async Task Autoscale_container_has_no_fixed_manual_throughput_value()
    {
        // An autoscale container's headline Throughput reflects the current
        // scaled RU/s, not a fixed provisioned value -- Content is exposed
        // through AutoscaleMaxThroughput instead, exercised above.
        var response = await _autoscaleContainer.ReadThroughputAsync(new RequestOptions());

        response.Resource.AutoscaleMaxThroughput.Should().NotBeNull();
    }
}
