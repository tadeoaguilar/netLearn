using System.Net;
using CosmosIndexingAndThroughput.Data;
using CosmosIndexingAndThroughput.Indexing;
using CosmosIndexingAndThroughput.Metrics;
using CosmosIndexingAndThroughput.Models;
using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Tests;

[Collection(nameof(CosmosCollection))]
public class IndexingPolicyTests : IAsyncLifetime
{
    private const string CustomPolicyContainerName = "orders-indexing-tests-custom";
    private const string DefaultPolicyContainerName = "orders-indexing-tests-default";

    private readonly CosmosFixture _fixture;
    private Container _customPolicyContainer = null!;
    private Container _defaultPolicyContainer = null!;

    public IndexingPolicyTests(CosmosFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var customResponse = await _fixture.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(CustomPolicyContainerName, "/customerId")
            {
                IndexingPolicy = IndexingPolicyFactory.CreateOrdersIndexingPolicy(),
            });
        _customPolicyContainer = customResponse.Container;

        var defaultResponse = await _fixture.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(DefaultPolicyContainerName, "/customerId"));
        _defaultPolicyContainer = defaultResponse.Container;
    }

    public async Task DisposeAsync()
    {
        await _customPolicyContainer.DeleteContainerAsync();
        await _defaultPolicyContainer.DeleteContainerAsync();
    }

    [Fact]
    public async Task Custom_policy_excludes_the_orderLines_subtree()
    {
        var properties = (await _customPolicyContainer.ReadContainerAsync()).Resource;

        properties.IndexingPolicy.ExcludedPaths
            .Should().Contain(p => p.Path == "/orderLines/*");
    }

    [Fact]
    public async Task Custom_policy_has_a_composite_index_on_customerId_and_orderDate()
    {
        var properties = (await _customPolicyContainer.ReadContainerAsync()).Resource;

        properties.IndexingPolicy.CompositeIndexes.Should().ContainSingle(composite =>
            composite.Count == 2 &&
            composite[0].Path == "/customerId" && composite[0].Order == CompositePathSortOrder.Ascending &&
            composite[1].Path == "/orderDate" && composite[1].Order == CompositePathSortOrder.Descending);
    }

    [Fact]
    public async Task Custom_policy_still_indexes_top_level_properties()
    {
        var properties = (await _customPolicyContainer.ReadContainerAsync()).Resource;

        properties.IndexingPolicy.IncludedPaths.Should().Contain(p => p.Path == "/*");
    }

    [Fact]
    public async Task Indexing_policy_round_trips_through_ReplaceContainerAsync()
    {
        // Start from a default policy, then replace it -- mirrors the
        // exercise's Part 2, where an existing container's policy is
        // changed after the fact rather than set only at creation time.
        var properties = (await _defaultPolicyContainer.ReadContainerAsync()).Resource;
        properties.IndexingPolicy.ExcludedPaths.Should().NotContain(p => p.Path == "/orderLines/*");

        properties.IndexingPolicy = IndexingPolicyFactory.CreateOrdersIndexingPolicy();
        await _defaultPolicyContainer.ReplaceContainerAsync(properties);

        var replaced = (await _defaultPolicyContainer.ReadContainerAsync()).Resource;
        replaced.IndexingPolicy.ExcludedPaths.Should().Contain(p => p.Path == "/orderLines/*");
        replaced.IndexingPolicy.CompositeIndexes.Should().ContainSingle(composite =>
            composite.Count == 2 && composite[0].Path == "/customerId" && composite[1].Path == "/orderDate");
    }

    [Fact]
    public async Task Composite_index_orderby_returns_results_sorted_by_customerId_then_orderDate_desc()
    {
        var customerA = "customer-A";
        var customerB = "customer-B";
        await SeedOrderAsync(_customPolicyContainer, customerA, DateTimeOffset.UtcNow.AddDays(-3));
        await SeedOrderAsync(_customPolicyContainer, customerA, DateTimeOffset.UtcNow.AddDays(-1));
        await SeedOrderAsync(_customPolicyContainer, customerB, DateTimeOffset.UtcNow.AddDays(-2));

        var (results, _) = await _customPolicyContainer.ExecuteAndMeasureAsync<Order>(
            new QueryDefinition("SELECT * FROM o WHERE o.customerId IN (@a, @b) ORDER BY o.customerId ASC, o.orderDate DESC")
                .WithParameter("@a", customerA)
                .WithParameter("@b", customerB));

        results.Should().HaveCountGreaterOrEqualTo(3);

        for (var i = 1; i < results.Count; i++)
        {
            var previous = results[i - 1];
            var current = results[i];
            if (previous.CustomerId == current.CustomerId)
            {
                current.OrderDate.Should().BeOnOrBefore(previous.OrderDate);
            }
            else
            {
                string.CompareOrdinal(previous.CustomerId, current.CustomerId).Should().BeLessOrEqualTo(0);
            }
        }
    }

    [Fact]
    public async Task Orderby_on_two_properties_fails_without_a_matching_composite_index()
    {
        Func<Task> act = () => _defaultPolicyContainer.ExecuteAndMeasureAsync<Order>(
            new QueryDefinition("SELECT * FROM o ORDER BY o.customerId ASC, o.orderDate DESC"));

        var exception = await act.Should().ThrowAsync<CosmosException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Excluded_path_query_still_returns_the_correct_results_via_scan()
    {
        var customerId = "customer-excluded-path";
        var order = SampleOrderFactory.Create(customerId, Guid.NewGuid().ToString());
        await _customPolicyContainer.UpsertItemAsync(order, new PartitionKey(customerId));

        var (matches, _) = await _customPolicyContainer.ExecuteAndMeasureAsync<Order>(
            new QueryDefinition("SELECT VALUE o FROM o JOIN l IN o.orderLines WHERE l.productId = @productId")
                .WithParameter("@productId", SampleOrderFactory.DemoProductId),
            new QueryRequestOptions { PartitionKey = new PartitionKey(customerId) });

        matches.Should().Contain(o => o.Id == order.Id);
    }

    private static async Task SeedOrderAsync(Container container, string customerId, DateTimeOffset orderDate)
    {
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            OrderDate = orderDate,
            Status = "Pending",
            OrderLines = new List<OrderLine>
            {
                new() { ProductId = "P001", ProductName = "Test Product", Quantity = 1, UnitPrice = 10m },
            },
            TotalAmount = 10m,
        };
        await container.UpsertItemAsync(order, new PartitionKey(customerId));
    }
}
