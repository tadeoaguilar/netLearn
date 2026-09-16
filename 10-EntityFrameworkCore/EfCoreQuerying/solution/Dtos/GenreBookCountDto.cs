namespace EfCoreQuerying.Dtos;

/// <summary>Result of grouping books by genre. See EXERCISE.md Part 5.</summary>
public record GenreBookCountDto(string GenreName, int BookCount);
