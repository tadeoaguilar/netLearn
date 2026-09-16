namespace EfCoreQuerying.Dtos;

/// <summary>
/// A flat projection of a book -- exactly the columns a book list page needs,
/// nothing else. No Author/Publisher entities, no tracked change-detection
/// snapshots, no relational fix-up. See EXERCISE.md Part 2.
/// </summary>
public record BookSummaryDto(
    int Id,
    string Title,
    string AuthorName,
    string PublisherName,
    decimal Price,
    DateOnly PublishedOn);
