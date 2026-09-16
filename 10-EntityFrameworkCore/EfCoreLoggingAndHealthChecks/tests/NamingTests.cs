using EfCoreLoggingAndHealthChecks.Data;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLoggingAndHealthChecks.Tests;

/// <summary>
/// The schema is part of the contract: anyone writing a report, a migration or
/// a psql query depends on these names. Pinning them here means a rename is a
/// deliberate act rather than an accident. Mirrors module 09's NamingTests --
/// same convention, same class copied verbatim into this project's Data folder.
/// </summary>
public class NamingTests
{
    [Theory]
    [InlineData("Id", "id")]
    [InlineData("Name", "name")]
    [InlineData("ProductId", "product_id")]
    [InlineData("CreatedAt", "created_at")]
    [InlineData("UpdatedAt", "updated_at")]
    [InlineData("PK_products", "pk_products")]
    [InlineData("IX_orders_ProductId", "ix_orders_product_id")]
    [InlineData("FK_orders_products_ProductId", "fk_orders_products_product_id")]
    public void Names_are_converted_to_snake_case(string input, string expected)
    {
        SnakeCaseNaming.ToSnakeCase(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("TaskDTOId", "task_dto_id")]
    [InlineData("HTTPStatus", "http_status")]
    [InlineData("Address1", "address1")]
    public void Runs_of_capitals_are_not_split_letter_by_letter(string input, string expected)
    {
        SnakeCaseNaming.ToSnakeCase(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Null_or_empty_names_pass_through_unchanged(string? input)
    {
        SnakeCaseNaming.ToSnakeCase(input).Should().Be(input);
    }

    [Fact]
    public void Every_mapped_column_in_the_model_is_snake_case()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=design-time-only")
            .Options;

        using var context = new AppDbContext(options);

        var columns = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Select(p => p.GetColumnName())
            .ToArray();

        columns.Should().NotBeEmpty();
        columns.Should().OnlyContain(name => name == name!.ToLowerInvariant(),
            "a PascalCase column can only be referenced quoted in PostgreSQL");
        columns.Should().NotContain(name => name!.Contains("__"));
    }
}
