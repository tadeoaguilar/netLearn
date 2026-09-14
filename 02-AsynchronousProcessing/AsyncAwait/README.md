# AsyncAwait

## Overview

`async`/`await` is how .NET stops a thread from sitting idle while it waits for
something slow. Get it right and a handful of threads serve thousands of
concurrent operations. Get it wrong and you get deadlocks, swallowed
exceptions, and code that is somehow slower than the synchronous version.

This project covers the mechanics and the traps.

## What You'll Learn

| Part | Topic | The point |
|---|---|---|
| 1 | Sync vs async | Blocking a thread vs releasing it — 6000ms vs 2000ms |
| 2 | `WhenAll` / `WhenAny` | Combining tasks, and why results come back in argument order |
| 3 | Cancellation tokens | Cancellation is cooperative; an unpassed token does nothing |
| 4 | Exception handling | `await` surfaces only the *first* exception from `WhenAll` |
| 5 | `ConfigureAwait` | What a synchronization context is and when capturing it hurts |
| 6 | Pitfalls | `async void`, sync-over-async, fire-and-forget, `Task.Run` for I/O |
| 7 | Data processor | Concurrency, cancellation and timeout in one realistic piece |
| — | Challenge | A download manager: retry, per-item timeout, progress, statistics |

## Key Concepts

### Async is not parallelism

Async is about **not blocking a thread while waiting**. Parallelism is about
**using more cores to compute faster**. Async suits I/O — network, disk,
database. Parallelism suits CPU work, and is the subject of the next project,
[TaskParallelLibrary](../TaskParallelLibrary/).

Reaching for `Task.Run` around async I/O gets you the costs of both and the
benefit of neither.

### Starting a task is not awaiting it

```csharp
// Sequential: 6000ms. Each await finishes before the next call starts.
var a = await DownloadAsync(url1);
var b = await DownloadAsync(url2);

// Concurrent: ~2000ms. Both are in flight before either is awaited.
var taskA = DownloadAsync(url1);
var taskB = DownloadAsync(url2);
await Task.WhenAll(taskA, taskB);
```

### Cancellation only works if you pass the token on

```csharp
await Task.Delay(5000);              // ignores cancellation entirely
await Task.Delay(5000, token);       // cancels the wait itself
```

A `CancellationToken` you accept and never forward is the most common reason
for "cancellation doesn't do anything" bug reports.

### `await` hides every exception but the first

`await Task.WhenAll(tasks)` throws the first failure it finds. The others are
still recorded on their own tasks — read `whenAllTask.Exception.InnerExceptions`
to see them all. Losing exceptions this way is silent and easy.

## Common Pitfalls

1. **`async void`** — the caller cannot await it and cannot catch its
   exceptions. Only for genuine event handlers.
2. **`.Result` / `.Wait()`** — deadlocks anywhere a synchronization context
   exists. Async all the way up instead.
3. **Fire-and-forget** — an unawaited task may not have finished, and its
   exception goes nowhere.
4. **`Task.Run` around async I/O** — burns a pool thread waiting for something
   that never needed one.
5. **Not passing `CancellationToken` through** — see above.

## Running This Project

```bash
dotnet run --project AsyncAwait              # your workspace
dotnet run --project solution -- all         # the reference solution
dotnet test tests                            # 19 tests
```

Parts 1-3 and 7 take a few seconds on purpose — the elapsed time is the lesson.

## Prerequisites

- [Module 1: Dependency Injection](../../01-DependencyInjection/) — the
  challenge uses constructor injection and an interface seam to stay testable
- Comfortable with C# lambdas and generics

## Next

[TaskParallelLibrary](../TaskParallelLibrary/) — for work that is CPU-bound
rather than I/O-bound.
