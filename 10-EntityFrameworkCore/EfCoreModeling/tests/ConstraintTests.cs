using EfCoreModeling.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCoreModeling.Tests;

/// <summary>
/// Part 7: indexes and constraints -- a unique index, a check constraint,
/// and required/max-length strings.
/// </summary>
public class ConstraintTests
{
    [Fact]
    public void Review_rating_has_a_check_constraint_between_1_and_5()
    {
        // Check constraints are stripped from the runtime-optimized
        // context.Model (they're never needed to answer a query), so
        // inspecting them means asking for the design-time model instead --
        // still built entirely in memory, no connection involved.
        using var context = TestDbContextFactory.Create();
        var model = context.GetService<IDesignTimeModel>().Model;
        var review = model.FindEntityType(typeof(Review))!;

        var checkConstraints = review.GetCheckConstraints().ToArray();

        checkConstraints.Should().ContainSingle(c => c.Sql!.Contains("rating"));
    }

    [Fact]
    public void Genre_name_has_a_unique_index()
    {
        using var context = TestDbContextFactory.Create();
        var genre = context.Model.FindEntityType(typeof(Genre))!;

        genre.GetIndexes().Should().Contain(i => i.IsUnique);
    }

    [Fact]
    public void Author_name_is_required_and_length_bounded()
    {
        using var context = TestDbContextFactory.Create();
        var author = context.Model.FindEntityType(typeof(Author))!;

        var name = author.FindProperty(nameof(Author.Name))!;

        name.IsNullable.Should().BeFalse();
        name.GetMaxLength().Should().Be(200);
    }

    [Fact]
    public void Author_bio_is_optional()
    {
        using var context = TestDbContextFactory.Create();
        var author = context.Model.FindEntityType(typeof(Author))!;

        var bio = author.FindProperty(nameof(Author.Bio))!;

        bio.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void Book_title_is_required_and_length_bounded()
    {
        using var context = TestDbContextFactory.Create();
        var book = context.Model.FindEntityType(typeof(Book))!;

        var title = book.FindProperty(nameof(Book.Title))!;

        title.IsNullable.Should().BeFalse();
        title.GetMaxLength().Should().Be(300);
    }

    [Fact]
    public void Review_reviewerName_is_required_and_length_bounded()
    {
        using var context = TestDbContextFactory.Create();
        var review = context.Model.FindEntityType(typeof(Review))!;

        var reviewerName = review.FindProperty(nameof(Review.ReviewerName))!;

        reviewerName.IsNullable.Should().BeFalse();
        reviewerName.GetMaxLength().Should().Be(200);
    }
}
