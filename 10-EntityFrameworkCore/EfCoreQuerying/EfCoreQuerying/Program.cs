// EfCoreQuerying -- your workspace.
//
// This is where you write the code from EXERCISE.md. Work through the parts
// in order, replacing this file (and adding files under Domain/, Persistence/,
// Seed/, Dtos/, Queries/ and Demos/) as you go:
//
//   Part 1  Basic LINQ (Where/OrderBy/Skip/Take) . Demos/Part1BasicLinq.cs
//   Part 2  Projections vs. tracked entities ..... Demos/Part2Projections.cs
//   Part 3  Include/ThenInclude vs. projection ... Demos/Part3Includes.cs
//   Part 4  AsSplitQuery ......................... Demos/Part4SplitQuery.cs
//   Part 5  GroupBy and aggregates ............... Demos/Part5GroupBy.cs
//   Part 6  Client vs. server evaluation ......... Demos/Part6ClientEval.cs
//   Part 7  Raw SQL .............................. Demos/Part7RawSql.cs
//   Part 8  PostgreSQL-only features ............. Demos/Part8PostgresFeatures.cs
//   Part 9  Compiled queries ..................... Demos/Part9CompiledQuery.cs
//
// Before any of that will run, you need the domain model (Domain/), the
// DbContext (Persistence/), and a seed (Seed/) -- see EXERCISE.md Part 0.
//
// appsettings.json already has a connection string pointing at the
// "efcore_querying" database started by ../../docker-compose.yml.
//
// Run your work:        dotnet run
// Check it:              dotnet test ../tests
// Compare afterwards:   ../solution/
//
// Try each part yourself before opening the reference solution. Reading it
// first is the fastest way to feel productive and learn nothing.

Console.WriteLine("EfCoreQuerying workspace -- start with Part 0 of EXERCISE.md");
