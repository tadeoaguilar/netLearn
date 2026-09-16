using EfCoreQuerying.Domain;
using EfCoreQuerying.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreQuerying.Seed;

/// <summary>
/// Deterministic seed data for the library catalog -- ~90 books spread across
/// a dozen authors, half a dozen publishers, and eight genres, each with tags,
/// JSONB metadata, and a handful of reviews. Deterministic because it is
/// driven by a fixed <see cref="Random"/> seed, so every run (and every test
/// run against a fresh Testcontainers database) produces exactly the same
/// rows -- which is what lets EXERCISE.md and the tests assert on concrete
/// numbers ("the average rating for book 3 is exactly 4.0") instead of
/// "some number greater than zero".
/// </summary>
public static class LibrarySeeder
{
    private static readonly string[] AuthorNames =
    [
        "Ada Whitfield", "Marcus Chen", "Priya Natarajan", "Elena Voss",
        "Tomás Rivera", "Grace Kowalski", "Kwame Osei", "Linnea Sørensen",
        "Hiroshi Tanaka", "Fatima Al-Rashid", "Declan Murphy", "Yuki Sato",
    ];

    private static readonly string[] PublisherNames =
    [
        "Northwind Press", "Blackwood & Fen", "Quill & Compass",
        "Ironleaf Books", "Starling House", "Meridian Editions",
    ];

    private static readonly string[] GenreNames =
    [
        "Science Fiction", "Fantasy", "Mystery", "Non-Fiction",
        "Biography", "History", "Horror", "Romance",
    ];

    private static readonly string[] TagPool =
    [
        "scifi", "space", "dystopian", "magic", "epic", "detective",
        "noir", "cozy", "true-story", "memoir", "war", "ancient",
        "gothic", "supernatural", "slow-burn", "enemies-to-lovers",
        "bestseller", "debut", "translated", "illustrated",
    ];

    private static readonly string[] Languages = ["en", "es", "fr", "de", "ja"];

    private static readonly string[] TitleNouns =
    [
        "Signal", "Garden", "Archive", "Harbor", "Cipher", "Lantern",
        "Meridian", "Orchard", "Threshold", "Reliquary", "Compass",
        "Ember", "Chronicle", "Labyrinth", "Vigil", "Tideline",
        "Wintering", "Paperweight", "Skyline", "Undertow",
    ];

    private static readonly string[] TitleAdjectives =
    [
        "Last", "Hidden", "Quiet", "Broken", "Distant", "Forgotten",
        "Silent", "Crimson", "Endless", "Fractured", "Nameless",
        "Wandering", "Unwritten", "Brilliant", "Gray",
    ];

    public static async Task SeedAsync(LibraryDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Books.AnyAsync(cancellationToken))
        {
            return; // Already seeded -- keeps this idempotent across re-runs.
        }

        var random = new Random(20260915); // Fixed seed: same data every run.

        var authors = AuthorNames.Select(name => new Author { Name = name }).ToList();
        var publishers = PublisherNames.Select(name => new Publisher { Name = name }).ToList();
        var genres = GenreNames.Select(name => new Genre { Name = name }).ToList();

        context.Authors.AddRange(authors);
        context.Publishers.AddRange(publishers);
        context.Genres.AddRange(genres);
        await context.SaveChangesAsync(cancellationToken);

        var books = new List<Book>();
        const int bookCount = 90;

        for (var i = 0; i < bookCount; i++)
        {
            var adjective = TitleAdjectives[random.Next(TitleAdjectives.Length)];
            var noun = TitleNouns[random.Next(TitleNouns.Length)];
            var title = $"The {adjective} {noun}";

            var author = authors[random.Next(authors.Count)];
            var publisher = publishers[random.Next(publishers.Count)];

            var genreCount = random.Next(1, 3); // 1 or 2 genres per book
            var bookGenres = genres.OrderBy(_ => random.Next()).Take(genreCount).ToList();

            var tagCount = random.Next(1, 5); // 1-4 tags
            var tags = TagPool.OrderBy(_ => random.Next()).Take(tagCount).ToArray();

            var publishedOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-random.Next(365, 365 * 25)));
            var price = Math.Round((decimal)(random.NextDouble() * 45 + 5), 2);
            var pages = random.Next(120, 820);
            var language = Languages[random.Next(Languages.Length)];
            var featured = random.Next(0, 5) == 0; // ~20% featured

            var metadata = $$"""
                {"language":"{{language}}","pages":{{pages}},"featured":{{(featured ? "true" : "false")}}}
                """;

            var book = new Book
            {
                Title = title,
                Isbn = $"978-0-{(100000 + i):D6}-0",
                PublishedOn = publishedOn,
                Price = price,
                Author = author,
                Publisher = publisher,
                Tags = tags,
                Metadata = metadata,
                Genres = bookGenres,
            };

            var reviewCount = random.Next(0, 7); // 0-6 reviews
            for (var r = 0; r < reviewCount; r++)
            {
                book.Reviews.Add(new Review
                {
                    Rating = random.Next(1, 6),
                    Comment = random.Next(0, 3) == 0 ? null : $"Review #{r + 1} for \"{title}\".",
                });
            }

            books.Add(book);
        }

        context.Books.AddRange(books);
        await context.SaveChangesAsync(cancellationToken);
    }
}
