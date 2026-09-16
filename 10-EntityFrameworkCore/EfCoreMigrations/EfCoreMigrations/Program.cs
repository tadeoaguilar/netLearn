// EfCoreMigrations -- your workspace.
//
// This is where you build the Author/Book schema from EXERCISE.md and evolve
// it, one real migration at a time. Work through the parts in order:
//
//   Part 1  Initial model + first migration ... Models/Author.cs, Models/Book.cs
//                                                Data/AppDbContext.cs
//                                                Data/AppDbContextFactory.cs
//                                                Data/SnakeCaseNaming.cs
//                                                Data/Configurations/*.cs
//                                                dotnet ef migrations add InitialCreate
//   Part 2  Add a column ......................  dotnet ef migrations add AddBookPageCount
//   Part 3  Rename a column, by hand ..........  dotnet ef migrations add RenameBookPagesColumn
//                                                (then check/hand-edit the generated file)
//   Part 4  Index + check constraint ..........  dotnet ef migrations add AddIndexAndBookRatingConstraint
//   Part 5  Two ways to seed data .............  Data/SeedData.cs
//                                                dotnet ef migrations add SeedGenreReferenceData
//   Part 6  Applying and rolling back .........  no new migration -- discussion + practice
//
// There is deliberately no Migrations/ folder here yet -- you create it by
// running `dotnet ef migrations add` yourself, exactly as EXERCISE.md walks
// through. Unlike a plain console app, nothing here runs until you've built
// at least Part 1's model and DbContext.
//
// Run your work:        dotnet run
// Generate migrations:  dotnet ef migrations add <Name> -o Migrations
// Check it:             dotnet test ../tests
// Compare afterwards:   ../solution/
//
// Try each part yourself before opening the reference solution. Reading it
// first is the fastest way to feel productive and learn nothing.

Console.WriteLine("EfCoreMigrations workspace -- start with Part 1 of EXERCISE.md");
