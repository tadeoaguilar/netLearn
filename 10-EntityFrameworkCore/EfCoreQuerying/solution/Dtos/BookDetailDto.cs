namespace EfCoreQuerying.Dtos;

/// <summary>
/// A book plus its related data, projected directly instead of loaded via
/// Include/ThenInclude. See EXERCISE.md Part 3.
/// </summary>
public record BookDetailDto(
    int Id,
    string Title,
    string AuthorName,
    string PublisherName,
    IReadOnlyList<string> GenreNames,
    IReadOnlyList<string> ReviewComments,
    double? AverageRating);
