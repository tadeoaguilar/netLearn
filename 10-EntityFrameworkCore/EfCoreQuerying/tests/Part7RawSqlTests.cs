using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Tests;

[Collection(PostgresCollection.Name)]
public class Part7RawSqlTests(PostgresFixture fixture)
{
    [Fact]
    public async Task FromSqlInterpolated_Composed_With_Where_Returns_Cheapest_Book_Per_Author()
    {
        await using var context = fixture.CreateContext();

        var minPrice = 0m;
        var cheapestPerAuthor = await context.Books
            .FromSqlInterpolated($"""
                select b.* from (
                    select b.*, row_number() over (partition by b.author_id order by b.price asc) as rn
                    from books b
                ) b
                where b.rn = 1 and b.price >= {minPrice}
                """)
            .OrderBy(b => b.Price)
            .ToListAsync();

        var authorCount = await context.Books.Select(b => b.AuthorId).Distinct().CountAsync();

        cheapestPerAuthor.Should().HaveCount(authorCount);
        // Each author appears exactly once in the "cheapest per author" result.
        cheapestPerAuthor.Select(b => b.AuthorId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ExecuteSqlInterpolatedAsync_Applies_A_Bulk_Discount_And_Can_Be_Rolled_Back()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var before = await context.Books
            .Where(b => b.Tags.Contains("bestseller"))
            .Select(b => b.Price)
            .ToListAsync();
        before.Should().NotBeEmpty();

        var rowsAffected = await context.Database.ExecuteSqlInterpolatedAsync(
            $"update books set price = round(price * 0.9, 2) where 'bestseller' = any(tags)");

        rowsAffected.Should().Be(before.Count);

        context.ChangeTracker.Clear();
        var after = await context.Books
            .Where(b => b.Tags.Contains("bestseller"))
            .OrderBy(b => b.Id)
            .Select(b => b.Price)
            .ToListAsync();

        after.Should().HaveCount(before.Count);
        for (var i = 0; i < before.Count; i++)
        {
            after[i].Should().BeApproximately(Math.Round(before.OrderBy(p => p).ElementAt(i) * 0.9m, 2), 0.01m);
        }

        // Roll back so this test doesn't permanently mutate data other tests rely on.
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Interpolated_Parameters_Are_Treated_As_Data_Not_As_SQL()
    {
        await using var context = fixture.CreateContext();

        var maliciousInput = "x'; DROP TABLE books; --";

        var results = await context.Books
            .FromSqlInterpolated($"select * from books where title ilike {"%" + maliciousInput + "%"}")
            .ToListAsync();

        results.Should().BeEmpty();

        // If the injection had "worked", this would throw because the table
        // would be gone. Interpolation parameterizes the value instead.
        var stillThere = await context.Books.CountAsync();
        stillThere.Should().BeGreaterThan(0);
    }
}
