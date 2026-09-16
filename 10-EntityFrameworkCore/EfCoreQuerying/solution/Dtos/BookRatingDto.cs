namespace EfCoreQuerying.Dtos;

/// <summary>Average rating and review count for one book. See EXERCISE.md Part 5.</summary>
public record BookRatingDto(int BookId, string Title, double AverageRating, int ReviewCount);
