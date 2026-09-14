# Channels

## Overview

`System.Threading.Channels` is the missing piece between async and parallelism:
an async-aware, thread-safe queue that lets producers and consumers run at
their own speeds without either losing data or exhausting memory.

If you have ever written a `BlockingCollection<T>`, a `ConcurrentQueue<T>` with
a polling loop, or a `SemaphoreSlim` guarding a `Queue<T>` — this replaces all
three, and does it without blocking a thread.

## What You'll Learn

| Part | Topic | The point |
|---|---|---|
| 1 | Basic producer/consumer | `WriteAsync`, `ReadAllAsync`, and why `Complete()` matters |
| 2 | Bounded channels | Backpressure, and the four `FullMode` strategies |
| 3 | Many producers, many consumers | Fan-in, fan-out, and load balancing |
| 4 | Pipelines | Chaining channels so every stage runs at once |
| 5 | Cancellation and errors | `Complete(exception)` and propagating failure downstream |
| 6 | Log processor | The real-world pattern: never block the hot path |
| — | Challenge | A four-stage message pipeline with statistics |

## Key Concepts

### Forgetting `Complete()` hangs your program

```csharp
await foreach (var item in channel.Reader.ReadAllAsync())
```

This loop ends when the writer is completed — and **only** then. Miss the
`Complete()` call and the consumer waits forever for a message that is never
coming. It is the single most common channel bug, and
`tests/ChannelBehaviourTests.cs` pins it down explicitly.

With multiple producers, complete the writer *after* all of them finish — not
inside one of them, which would cut the others off mid-write.

### Bounded channels are the point

```csharp
Channel.CreateUnbounded<T>()      // a fast producer grows this without limit
Channel.CreateBounded<T>(100)     // a fast producer waits instead
```

An unbounded channel with a producer faster than its consumer is a memory leak
with extra steps. Bounded capacity converts that into **backpressure**: the
producer's `WriteAsync` simply does not complete until there is room.

### The four full modes

| `FullMode` | Behaviour | Use for |
|---|---|---|
| `Wait` | Producer waits for room | Work you must not lose |
| `DropWrite` | Discard the incoming item | Best-effort, keep the earliest |
| `DropOldest` | Discard the queued head | Telemetry, metrics — stale data is useless |
| `DropNewest` | Discard the queued tail | Rare; keep the earliest arrivals |

With any drop mode, `TryWrite` always succeeds — something else quietly left
the buffer to make room.

### Many consumers means load balancing, not broadcast

Every consumer reading the same `ChannelReader` competes for items. Each item
goes to exactly **one** of them. If you need every consumer to see every item,
a channel is the wrong tool.

### Failure propagates through `Complete(exception)`

```csharp
channel.Writer.Complete(ex);   // consumers learn it BROKE
channel.Writer.Complete();     // consumers think it FINISHED
```

The difference matters: a plain `Complete()` after a failure makes a crashed
producer look like a successful one, and the error vanishes.

## Common Pitfalls

1. **No `Complete()`** — the consumer hangs forever
2. **`Complete()` inside one of several producers** — cuts the others off
3. **Unbounded channel, fast producer** — unbounded memory growth
4. **Plain `Complete()` on the error path** — silently swallows the failure
5. **Cancelling instead of completing** — throws away whatever was still buffered

## Running This Project

```bash
dotnet run --project Channels               # your workspace
dotnet run --project solution -- all        # the reference solution
dotnet test tests                           # 18 tests
```

## Note on `EXERCISE.md`

Part 5's consumer catches `ChannelClosedException` to observe a faulted
producer. That never fires — `ReadAllAsync` rethrows the **original** exception
passed to `Complete(ex)`. `ChannelClosedException` is what you get for *writing*
to a completed channel, the opposite direction. The reference solution has the
corrected version, and both behaviours are covered by tests.

## Prerequisites

- [AsyncAwait](../AsyncAwait/) — `await foreach`, cancellation tokens
- [TaskParallelLibrary](../TaskParallelLibrary/) — helpful for Part 3

## Next

This completes [Module 2](../). Next is
[Module 3: Clean Architecture](../../03-CleanArchitecture/).
