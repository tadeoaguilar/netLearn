# TaskParallelLibrary

## Overview

The previous project was about **not blocking a thread while waiting**. This one
is about **using every core you have to finish sooner**. They sound similar and
are solved by completely different tools.

`Parallel.For`, `Parallel.ForEach` and PLINQ split CPU-bound work across
threads. Used on the wrong workload they make things slower, not faster.

## What You'll Learn

| Part | Topic | The point |
|---|---|---|
| 1 | `Parallel.For` / `ForEach` | Splitting a loop across cores |
| 2 | PLINQ | `AsParallel()`, ordering costs, and when it loses to plain LINQ |
| 3 | Degree of parallelism | Capping concurrency when a shared resource is the bottleneck |
| 4 | Exception handling | `AggregateException`, and why you usually want per-item `try` |
| 5 | Cancellation | `ParallelOptions.CancellationToken`, and what happens to partial results |
| 6 | Partitioning | Chunk size as a trade between balance and coordination cost |
| 7 | Async vs parallel | The decision that matters more than any other in this module |
| — | Challenge | A parallel image processor: progress, failures, cancellation, statistics |

## Key Concepts

### Async vs parallel

| Workload | Use | Why |
|---|---|---|
| HTTP calls, file I/O, database queries | `async`/`await` | The thread is released during the wait |
| Image processing, data transformation, maths | `Parallel` / PLINQ | More cores genuinely finish sooner |

Getting this backwards is the classic mistake:

```csharp
// WRONG: ten threads, all asleep, achieving nothing
Parallel.For(0, 10, _ => Thread.Sleep(1000));

// RIGHT: no thread held at all
await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Delay(1000)));
```

At ten items nobody notices. At ten thousand concurrent requests, the first
version exhausts the thread pool and the second is still fine.

### `++` is not atomic

```csharp
counter++;                            // WRONG under Parallel.For -- loses updates
Interlocked.Increment(ref counter);   // CORRECT
```

`counter++` is three operations — read, add, write — and two threads can
interleave between them. `tests/ParallelBehaviourTests.cs` demonstrates the
lost updates directly.

The same applies to collections: `List<T>` corrupts silently under concurrent
writes. Use `ConcurrentBag<T>` or `ConcurrentDictionary<K,V>`.

### PLINQ is not free

Partitioning and merging cost real time. On cheap per-item work, `AsParallel()`
is *slower* than plain LINQ. Part 2 measures a case where it loses. Measure
before reaching for it.

`AsParallel()` also does not preserve order unless you add `AsOrdered()`, and
`AsOrdered()` costs more again.

### Console output serializes everything

`Console.WriteLine` takes a process-wide lock. Put one inside a parallel loop
and you have re-serialized the loop — which is why the timings in a naive
benchmark often show no speedup at all.

## Common Pitfalls

1. **Parallelizing I/O** — use async instead
2. **Unsynchronized shared state** — `Interlocked`, or a concurrent collection
3. **Benchmarking in Debug** — always `-c Release`
4. **Console I/O inside the loop** — serializes the thing you are measuring
5. **Assuming more threads is faster** — past core count it is pure overhead

## Running This Project

```bash
dotnet run -c Release --project TaskParallelLibrary     # your workspace
dotnet run -c Release --project solution -- all         # the reference solution
dotnet test tests                                       # 18 tests
```

## Note on the Tests

The tests never assert that parallel was *faster*. Timing assertions fail on a
loaded machine and tell you nothing when they do. They assert **behaviour**
instead: every iteration runs exactly once, the concurrency cap holds, failures
are aggregated, cancellation propagates. That is a habit worth keeping.

## Prerequisites

- [AsyncAwait](../AsyncAwait/) — Part 7 contrasts the two directly

## Next

[Channels](../Channels/) — coordinating producers and consumers, which is where
async and parallelism meet.
