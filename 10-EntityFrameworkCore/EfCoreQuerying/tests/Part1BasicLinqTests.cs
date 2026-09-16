using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Tests;

[Collection(PostgresCollection.Name)]
public class Part1BasicLinqTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Where_And_OrderBy_Filters_And_Sorts_By_Price()
    {
        await using var context = fixture.CreateContext();

        var cheap = await context.Books
            .Where(b => b.Price < 15)
            .OrderBy(b => b.Price)
            .Select(b => b.Price)
            .ToListAsync();

        cheap.Should().NotBeEmpty();
        cheap.Should().OnlyContain(p => p < 15);
        cheap.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Skip_And_Take_Page_Through_Results_Without_Overlap()
    {
        await using var context = fixture.CreateContext();

        const int pageSize = 10;

        var pageOne = await context.Books.OrderBy(b => b.Id).Skip(0).Take(pageSize).Select(b => b.Id).ToListAsync();
        var pageTwo = await context.Books.OrderBy(b => b.Id).Skip(pageSize).Take(pageSize).Select(b => b.Id).ToListAsync();

        pageOne.Should().HaveCount(pageSize);
        pageTwo.Should().HaveCount(pageSize);
        pageOne.Should().NotIntersectWith(pageTwo);
    }
}
