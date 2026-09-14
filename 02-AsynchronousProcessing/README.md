# Module 2: Asynchronous Processing

## Overview
Master asynchronous programming in .NET to build high-performance, scalable applications. Learn async/await, parallel processing, and modern concurrency patterns.

## Learning Objectives
- Understand async/await and Task-based programming
- Implement parallel processing efficiently
- Use channels for producer-consumer scenarios
- Handle cancellation and timeouts properly
- Avoid common async pitfalls

## Projects

### AsyncAwait
**What you'll learn:**
- Async/await fundamentals
- Task vs ValueTask
- Async all the way (avoiding sync-over-async)
- ConfigureAwait usage
- Cancellation tokens
- Error handling in async code

**Exercises:**
1. Convert synchronous I/O operations to async
2. Implement timeout and cancellation
3. Handle multiple async operations (WhenAll, WhenAny)
4. Fix deadlock scenarios

### TaskParallelLibrary
**What you'll learn:**
- Parallel.For and Parallel.ForEach
- PLINQ (Parallel LINQ)
- Task coordination (WaitAll, WhenAll)
- Degree of parallelism control
- Partitioning strategies

**Exercises:**
1. Parallelize CPU-bound operations
2. Compare sequential vs parallel performance
3. Implement custom partitioning
4. Balance workload across cores

### Channels
**What you'll learn:**
- System.Threading.Channels API
- Producer-consumer patterns
- Bounded vs unbounded channels
- Backpressure handling
- Multiple producers/consumers

**Exercises:**
1. Build a message processing pipeline
2. Implement rate limiting with channels
3. Create a work queue system
4. Handle backpressure scenarios

## Running This Module

```bash
# From the repository root
dotnet build netLearn.sln          # all projects in the module
dotnet test netLearn.sln           # all 55 tests in the module
```

Each project holds a workspace where you write code, a `solution/` folder with a
reference implementation, and `tests/` proving the behaviour. Work the exercise
first, then compare.

| Project | Run the reference | Tests |
|---|---|---|
| AsyncAwait | `dotnet run --project 02-AsynchronousProcessing/AsyncAwait/solution -- all` | 19 |
| TaskParallelLibrary | `dotnet run -c Release --project 02-AsynchronousProcessing/TaskParallelLibrary/solution -- all` | 18 |
| Channels | `dotnet run --project 02-AsynchronousProcessing/Channels/solution -- all` | 18 |

Use `-c Release` for TaskParallelLibrary — every timing figure in it is
meaningless in a Debug build.

## Async or Parallel?

The decision that matters most in this module, and the one people get backwards:

| Workload | Use | Why |
|---|---|---|
| HTTP calls, file I/O, database queries | `async`/`await` | The thread is released during the wait |
| Image processing, data transformation, maths | `Parallel` / PLINQ | More cores genuinely finish sooner |
| A fast producer and a slow consumer | `Channel<T>` | Backpressure instead of unbounded buffering |

## Corrections to the Exercises

Three errors in the exercise text are fixed in the reference solutions and
documented in each project's `GETTING_STARTED.md`:

- **AsyncAwait Part 5** — `LibraryMethodExample` is declared `async Task` but
  returns a value; it needs to be `async Task<string>`.
- **TaskParallelLibrary Part 1** — speedup is computed as `elapsed / elapsed`,
  which is always `1.00x`; and `Console.Write` inside the parallel loop
  re-serializes the very loop being measured.
- **Channels Part 5** — the consumer catches `ChannelClosedException`, which
  never fires. `ReadAllAsync` rethrows the original exception passed to
  `Complete(ex)`.

## Key Concepts

### Async/Await Pattern
```csharp
// Async method signature
public async Task<string> GetDataAsync()
{
    // Await asynchronous operation
    var result = await httpClient.GetStringAsync(url);
    return result;
}

// Calling async code
var data = await GetDataAsync();
```

### When to Use Async
- **Use async for**: I/O-bound operations (HTTP, database, file access)
- **Don't use async for**: CPU-bound synchronous code (use Task.Run or parallel)

### Common Pitfalls
1. **Async void** - Only use for event handlers
2. **Sync-over-async** - Avoid .Result or .Wait()
3. **Fire and forget** - Always await or handle tasks
4. **Deadlocks** - Understand synchronization context

### Task vs ValueTask
- Use `Task<T>` for most scenarios
- Use `ValueTask<T>` for hot paths with frequent synchronous completion

## Performance Considerations
- Async adds overhead - profile before optimizing
- Parallel processing benefits CPU-bound work
- Consider memory allocations in hot paths
- Use appropriate collection sizes for parallelism

## Best Practices
1. Async all the way (no blocking on async code)
2. Always pass CancellationToken to async methods
3. Use ConfigureAwait(false) in libraries
4. Handle exceptions in all async operations
5. Don't mix blocking and async code

## Prerequisites
- Completion of Module 1 (Dependency Injection)
- Understanding of C# delegates and lambdas
- Basic threading concepts

## Getting Started
Start with [AsyncAwait](AsyncAwait/), then move to [TaskParallelLibrary](TaskParallelLibrary/), and finish with [Channels](Channels/).

## Next Module
After completing this module, proceed to [03-CleanArchitecture](../03-CleanArchitecture/)
