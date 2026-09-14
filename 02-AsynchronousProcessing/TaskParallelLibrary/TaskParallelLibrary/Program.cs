// TaskParallelLibrary -- your workspace.
//
// Write the code from EXERCISE.md here, part by part:
//
//   Part 1  Parallel.For / ForEach .... Examples/BasicParallel.cs
//                                       Examples/ParallelForEach.cs
//   Part 2  PLINQ ..................... Examples/PlinqExample.cs
//   Part 3  Degree of parallelism ..... Examples/ParallelismControl.cs
//   Part 4  Exceptions ................ Examples/ParallelExceptions.cs
//   Part 5  Cancellation .............. Examples/ParallelCancellation.cs
//   Part 6  Partitioning .............. Examples/Partitioning.cs
//   Part 7  Async vs parallel ......... Examples/AsyncVsParallel.cs
//   Challenge  Image processor ........ your own design
//
// Run your work:        dotnet run -c Release
// Check it:             dotnet test ../tests
// Compare afterwards:   ../solution/
//
// Always use -c Release when you are measuring anything. A Debug build skips
// most JIT optimization, so Debug timings are not worth comparing.

Console.WriteLine($"TaskParallelLibrary workspace -- {Environment.ProcessorCount} logical cores available");
Console.WriteLine("Start with Part 1 of EXERCISE.md");
