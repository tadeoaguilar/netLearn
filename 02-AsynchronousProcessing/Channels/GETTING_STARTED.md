# Getting Started with Channels

## Quick Start

```bash
cd 02-AsynchronousProcessing/Channels
dotnet build ../../netLearn.sln
```

## What Is Already Here

```
Channels/
├── README.md                # The concepts
├── EXERCISE.md              # The work, in 6 parts plus a challenge
├── GETTING_STARTED.md       # This file
│
├── Channels/                # ← YOUR WORKSPACE. Write your code here.
│   ├── Program.cs
│   └── Examples/            #   empty, waiting for your files
│
├── solution/                # ← REFERENCE IMPLEMENTATION.
│   ├── Examples/            #   Parts 1-6
│   ├── Challenge/           #   the message processing system
│   └── Demos/               #   one runnable demo per part
│
└── tests/                   # ← 18 tests
```

## The Three Commands You Need

```bash
# Run your own work
dotnet run --project Channels

# Check your work
dotnet test tests

# See the reference solution, one part at a time
dotnet run --project solution -- 1           # Basic producer-consumer
dotnet run --project solution -- 2           # Bounded channels, backpressure
dotnet run --project solution -- 3           # Multiple producers and consumers
dotnet run --project solution -- 4           # Three-stage pipeline
dotnet run --project solution -- 5           # Cancellation and errors
dotnet run --project solution -- 6           # Background log processor
dotnet run --project solution -- challenge   # Message processing system
dotnet run --project solution -- all         # Everything in order
```

## If Your Program Hangs

You forgot `channel.Writer.Complete()`.

That is not a joke — it is the cause of most channel hangs. `ReadAllAsync` ends
when the writer is completed and at no other time, so a consumer with no
`Complete()` waits forever for a message that will never arrive.

Checklist when something hangs:

1. Is `Complete()` called on **every** path, including the `catch` block?
2. With several producers, is `Complete()` called **after all** of them finish,
   rather than inside one?
3. In a pipeline, does **each** stage complete its own output channel once its
   input is drained?
4. Did a stage throw before reaching its `Complete()` call?

`tests/ChannelBehaviourTests.cs` has a test named
`Without_Complete_the_reader_waits_forever` that reproduces it deliberately.

## Known Issue in EXERCISE.md

Part 5's `DemonstrateErrorHandling` has the consumer catch
`ChannelClosedException`:

```csharp
catch (ChannelClosedException ex)
{
    Console.WriteLine($"[Consumer] Channel closed: {ex.InnerException?.Message}");
}
```

That block never runs. `ReadAllAsync` rethrows the **original** exception you
passed to `Complete(ex)` — an `InvalidOperationException` in that example — not
a `ChannelClosedException` wrapping it.

`ChannelClosedException` is what you get when you **write** to a completed
channel. Two different directions, two different exceptions:

| Action | Exception |
|---|---|
| Read from a channel completed with an exception | that original exception |
| Write to a completed channel | `ChannelClosedException` |
| `await reader.Completion` on a faulted channel | that original exception |

All three are covered in `tests/ChannelBehaviourTests.cs`. The reference
solution has the corrected `catch`.

## A Note on the Challenge

`EXERCISE.md` specifies producer → validator → processor ×3 → storage, with
bounded channels and statistics. The reference puts interfaces in front of the
validator, transformer and store, which is what lets the tests prove things
like "a rejected message never reaches the processor" and "at most one message
per processor is in flight" — neither of which you can see from console output.

`MessageProcessingSystem.cs` also shows the subtle part: with three processors
sharing one reader, only the **last** one to finish may complete the output
channel. Doing it in each processor would cut the other two off.

## If You Get Stuck

1. Hanging? See above — it is `Complete()`.
2. Losing messages? Check your `FullMode` — a drop mode discards silently.
3. Exception vanishing? You probably called `Complete()` instead of `Complete(ex)`.
4. Compare against `solution/` — same namespaces and type names as the exercise.
