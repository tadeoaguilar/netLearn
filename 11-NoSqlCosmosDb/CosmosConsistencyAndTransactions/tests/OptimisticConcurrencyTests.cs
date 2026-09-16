using System.Net;
using CosmosConsistencyAndTransactions.Domain;
using Microsoft.Azure.Cosmos;

namespace CosmosConsistencyAndTransactions.Tests;

[Collection("Cosmos emulator")]
public class OptimisticConcurrencyTests(CosmosFixture fixture)
{
    private async Task<Customer> SeedCustomerAsync(int loyaltyPoints)
    {
        var customer = new Customer { Name = "OCC Test", Email = "occ@example.com", LoyaltyPoints = loyaltyPoints };
        await fixture.Customers.CreateItemAsync(customer, new PartitionKey(customer.Id));
        return customer;
    }

    [Fact]
    public async Task Reading_an_item_returns_a_non_empty_ETag()
    {
        var seeded = await SeedCustomerAsync(0);
        var read = await fixture.Customers.ReadItemAsync<Customer>(seeded.Id, new PartitionKey(seeded.Id));

        read.ETag.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Replace_with_the_current_ETag_succeeds_and_changes_the_ETag()
    {
        var seeded = await SeedCustomerAsync(10);
        var read = await fixture.Customers.ReadItemAsync<Customer>(seeded.Id, new PartitionKey(seeded.Id));

        read.Resource.LoyaltyPoints = 20;
        var replaced = await fixture.Customers.ReplaceItemAsync(
            read.Resource, read.Resource.Id, new PartitionKey(read.Resource.Id),
            new ItemRequestOptions { IfMatchEtag = read.ETag });

        replaced.Resource.LoyaltyPoints.Should().Be(20);
        replaced.ETag.Should().NotBe(read.ETag);
    }

    [Fact]
    public async Task Replace_with_a_stale_ETag_throws_412_PreconditionFailed()
    {
        var seeded = await SeedCustomerAsync(100);
        var staleRead = await fixture.Customers.ReadItemAsync<Customer>(seeded.Id, new PartitionKey(seeded.Id));

        // Someone else writes first, moving the server-side ETag.
        var freshRead = await fixture.Customers.ReadItemAsync<Customer>(seeded.Id, new PartitionKey(seeded.Id));
        freshRead.Resource.LoyaltyPoints = 150;
        await fixture.Customers.ReplaceItemAsync(
            freshRead.Resource, freshRead.Resource.Id, new PartitionKey(freshRead.Resource.Id),
            new ItemRequestOptions { IfMatchEtag = freshRead.ETag });

        // staleRead still carries the ORIGINAL ETag.
        staleRead.Resource.LoyaltyPoints = 200;
        var act = async () => await fixture.Customers.ReplaceItemAsync(
            staleRead.Resource, staleRead.Resource.Id, new PartitionKey(staleRead.Resource.Id),
            new ItemRequestOptions { IfMatchEtag = staleRead.ETag });

        var assertion = await act.Should().ThrowAsync<CosmosException>();
        assertion.Which.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Reload_and_retry_after_a_412_succeeds_and_preserves_both_writers_intent()
    {
        var seeded = await SeedCustomerAsync(1000);
        var readA = await fixture.Customers.ReadItemAsync<Customer>(seeded.Id, new PartitionKey(seeded.Id));
        var readB = await fixture.Customers.ReadItemAsync<Customer>(seeded.Id, new PartitionKey(seeded.Id));

        readA.Resource.LoyaltyPoints += 50;
        await fixture.Customers.ReplaceItemAsync(
            readA.Resource, readA.Resource.Id, new PartitionKey(readA.Resource.Id),
            new ItemRequestOptions { IfMatchEtag = readA.ETag });

        readB.Resource.LoyaltyPoints += 20;
        ItemResponse<Customer> retryResult;
        try
        {
            await fixture.Customers.ReplaceItemAsync(
                readB.Resource, readB.Resource.Id, new PartitionKey(readB.Resource.Id),
                new ItemRequestOptions { IfMatchEtag = readB.ETag });
            throw new InvalidOperationException("Expected a 412 that never happened.");
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            var reloaded = await fixture.Customers.ReadItemAsync<Customer>(seeded.Id, new PartitionKey(seeded.Id));
            reloaded.Resource.LoyaltyPoints += 20;
            retryResult = await fixture.Customers.ReplaceItemAsync(
                reloaded.Resource, reloaded.Resource.Id, new PartitionKey(reloaded.Resource.Id),
                new ItemRequestOptions { IfMatchEtag = reloaded.ETag });
        }

        retryResult.Resource.LoyaltyPoints.Should().Be(1000 + 50 + 20);
    }

    [Fact]
    public async Task Replace_without_any_IfMatchEtag_always_succeeds_last_writer_wins()
    {
        // Contrast case: omitting IfMatchEtag means no concurrency check at
        // all -- this is the "lost update" the rest of this class exists to
        // prevent.
        var seeded = await SeedCustomerAsync(5);
        var readA = await fixture.Customers.ReadItemAsync<Customer>(seeded.Id, new PartitionKey(seeded.Id));
        var readB = await fixture.Customers.ReadItemAsync<Customer>(seeded.Id, new PartitionKey(seeded.Id));

        readA.Resource.LoyaltyPoints = 100;
        await fixture.Customers.ReplaceItemAsync(readA.Resource, readA.Resource.Id, new PartitionKey(readA.Resource.Id));

        readB.Resource.LoyaltyPoints = 200;
        var finalWrite = await fixture.Customers.ReplaceItemAsync(readB.Resource, readB.Resource.Id, new PartitionKey(readB.Resource.Id));

        finalWrite.Resource.LoyaltyPoints.Should().Be(200); // A's update to 100 is silently lost.
    }
}
