// EfCoreModeling -- your workspace.
//
// This is where you write the code from EXERCISE.md. Work through the parts
// in order, creating these files as you go:
//
//   Part 1  DbContext + Author ........... Persistence/LibraryDbContext.cs
//                                          Configurations/AuthorConfiguration.cs
//   Part 2  One-to-many .................. Entities/Publisher.cs, Entities/Book.cs
//                                          Configurations/PublisherConfiguration.cs
//                                          Configurations/BookConfiguration.cs
//   Part 3  Owned type (Money) ........... Entities/Money.cs
//   Part 4  Value converter (Isbn) ....... Entities/Isbn.cs
//   Part 5  Many-to-many with a payload .. Entities/Genre.cs, Entities/BookGenre.cs
//                                          Configurations/GenreConfiguration.cs
//                                          Configurations/BookGenreConfiguration.cs
//   Part 6  Table-per-hierarchy .......... Entities/DigitalBook.cs
//                                          Configurations/DigitalBookConfiguration.cs
//   Part 7  Indexes and constraints ...... Entities/Review.cs
//                                          Configurations/ReviewConfiguration.cs
//   Part 8  Snake-case naming ............ Persistence/SnakeCaseNaming.cs
//
// appsettings.json already points at the "efcore_modeling" database the
// module's docker-compose.yml provisions -- though nothing in this exercise
// needs the database to actually be running. EF Core builds its model the
// first time something touches context.Model, without opening a connection.
//
// Run your work:        dotnet run
// Check it:              dotnet test ../tests
// Compare afterwards:    ../solution/
//
// Try each part yourself before opening the reference solution. Reading it
// first is the fastest way to feel productive and learn nothing.

Console.WriteLine("EfCoreModeling workspace -- start with Part 1 of EXERCISE.md");
