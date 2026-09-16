using CosmosQuerying.Domain;
using CosmosQuerying.Persistence;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmosQuerying.Tests;

[Collection(CosmosCollection.Name)]
public class Part4PaginationTests(CosmosFixture fixture)
{
    [Fact]
    public async Task FeedIterator_Pages_Through_All_Results_Without_Duplicates_Or_Gaps()
    {
        const int pageSize = 25;
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.id");
        using var iterator = fixture.Orders.GetItemQueryIterator<Order>(
            query, requestOptions: new QueryRequestOptions { MaxItemCount = pageSize });

        var seenIds = new List<string>();
        var pageCount = 0;
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            pageCount++;
            seenIds.AddRange(page.Select(o => o.Id));
        }

        var expectedTotal = await CountAsync();

        pageCount.Should().BeGreaterThan(1); // proves this actually paged, not one giant page
        seenIds.Should().HaveCount(expectedTotal);
        seenIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Continuation_Token_Resumes_A_Query_From_Where_It_Left_Off()
    {
        const int pageSize = 30;
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.id");

        using var firstIterator = fixture.Orders.GetItemQueryIterator<Order>(
            query, requestOptions: new QueryRequestOptions { MaxItemCount = pageSize });
        var firstPage = await firstIterator.ReadNextAsync();
        firstPage.ContinuationToken.Should().NotBeNullOrEmpty();

        using var resumedIterator = fixture.Orders.GetItemQueryIterator<Order>(
            query,
            continuationToken: firstPage.ContinuationToken,
            requestOptions: new QueryRequestOptions { MaxItemCount = pageSize });
        var secondPage = await resumedIterator.ReadNextAsync();

        var firstPageIds = firstPage.Select(o => o.Id).ToList();
        var secondPageIds = secondPage.Select(o => o.Id).ToList();

        secondPageIds.Should().NotBeEmpty();
        secondPageIds.Should().NotIntersectWith(firstPageIds);
    }

    [Fact]
    public async Task Last_Page_Has_A_Null_Continuation_Token()
    {
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.id");
        using var iterator = fixture.Orders.GetItemQueryIterator<Order>(
            query, requestOptions: new QueryRequestOptions { MaxItemCount = 1000 });

        FeedResponse<Order>? lastPage = null;
        while (iterator.HasMoreResults)
        {
            lastPage = await iterator.ReadNextAsync();
        }

        lastPage.Should().NotBeNull();
        lastPage!.ContinuationToken.Should().BeNull();
    }

    [Fact]
    public async Task SkipTake_Returns_The_Correct_Page_Of_A_Deterministic_Order()
    {
        var linqResults = await ToListAsync(
            fixture.Orders.GetItemLinqQueryable<Order>(linqSerializerOptions: CosmosSerialization.LinqOptions)
                .OrderBy(o => o.Id)
                .Skip(10)
                .Take(5));

        var allIdsSorted = (await AllIdsAsync()).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var expectedPage = allIdsSorted.Skip(10).Take(5).ToList();

        linqResults.Select(o => o.Id).Should().BeEquivalentTo(expectedPage, options => options.WithStrictOrdering());
    }

    private async Task<int> CountAsync()
    {
        using var iterator = fixture.Orders.GetItemQueryIterator<int>(new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));
        var total = 0;
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            total += page.FirstOrDefault();
        }

        return total;
    }

    private async Task<List<string>> AllIdsAsync()
    {
        using var iterator = fixture.Orders.GetItemQueryIterator<string>(new QueryDefinition("SELECT VALUE c.id FROM c"));
        var ids = new List<string>();
        while (iterator.HasMoreResults)
        {
            ids.AddRange(await iterator.ReadNextAsync());
        }

        return ids;
    }

    private static async Task<List<T>> ToListAsync<T>(IQueryable<T> queryable)
    {
        using var iterator = queryable.ToFeedIterator();
        var results = new List<T>();
        while (iterator.HasMoreResults)
        {
            results.AddRange(await iterator.ReadNextAsync());
        }

        return results;
    }
}
