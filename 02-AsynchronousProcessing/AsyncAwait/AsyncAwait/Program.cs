// AsyncAwait -- your workspace.
//
// Write the code from EXERCISE.md here, part by part:
//
//   Part 1  Sync vs async ............ Examples/SyncVsAsync.cs
//   Part 2  WhenAll / WhenAny ........ Examples/TaskCombinators.cs
//   Part 3  Cancellation ............. Examples/CancellationExample.cs
//   Part 4  Exception handling ....... Examples/ExceptionHandling.cs
//   Part 5  ConfigureAwait ........... Examples/ConfigureAwaitExample.cs
//   Part 6  Pitfalls ................. Examples/Pitfalls.cs
//   Part 7  Data processor ........... Examples/DataProcessor.cs
//   Challenge  Download manager ...... your own design
//
// Run your work:        dotnet run
// Check it:             dotnet test ../tests
// Compare afterwards:   ../solution/
//
// One repo-specific thing to know: this solution builds CS4014 (a task you
// never awaited) and CS1998 (an async method with no await) as ERRORS, not
// warnings. If you hit one, you have found a real bug -- read it, do not
// suppress it.

Console.WriteLine("AsyncAwait workspace -- start with Part 1 of EXERCISE.md");

await Task.CompletedTask;
