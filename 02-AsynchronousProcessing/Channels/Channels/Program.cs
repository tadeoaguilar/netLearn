// Channels -- your workspace.
//
// Write the code from EXERCISE.md here, part by part:
//
//   Part 1  Basic producer/consumer ... Examples/BasicChannel.cs
//   Part 2  Bounded and backpressure .. Examples/BoundedChannel.cs
//   Part 3  Many producers/consumers .. Examples/MultipleProducersConsumers.cs
//   Part 4  Pipelines ................. Examples/Pipeline.cs
//   Part 5  Cancellation and errors ... Examples/CancellationAndErrors.cs
//   Part 6  Log processor ............. Examples/LogProcessor.cs
//   Challenge  Message pipeline ....... your own design
//
// Run your work:        dotnet run
// Check it:             dotnet test ../tests
// Compare afterwards:   ../solution/
//
// If your program hangs and never exits, you almost certainly forgot
// channel.Writer.Complete(). It is the number one channel bug.

Console.WriteLine("Channels workspace -- start with Part 1 of EXERCISE.md");

await Task.CompletedTask;
