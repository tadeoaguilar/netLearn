# Getting Started with AsyncAwait

## Quick Start

```bash
cd 02-AsynchronousProcessing/AsyncAwait
dotnet build ../../netLearn.sln
```

## What Is Already Here

```
AsyncAwait/
├── README.md                # The concepts
├── EXERCISE.md              # The work, in 7 parts plus a challenge
├── GETTING_STARTED.md       # This file
│
├── AsyncAwait/              # ← YOUR WORKSPACE. Write your code here.
│   ├── Program.cs           #   replace as you work through each part
│   └── Examples/            #   empty, waiting for your files
│
├── solution/                # ← REFERENCE IMPLEMENTATION. Look after trying.
│   ├── Examples/            #   Parts 1-7
│   ├── Challenge/           #   the download manager
│   └── Demos/               #   one runnable demo per part
│
└── tests/                   # ← 19 tests, all running in under a second
```

## The Three Commands You Need

```bash
# Run your own work
dotnet run --project AsyncAwait

# Check your work
dotnet test tests

# See the reference solution, one part at a time
dotnet run --project solution -- 1           # Sync vs async
dotnet run --project solution -- 2           # WhenAll / WhenAny
dotnet run --project solution -- 3           # Cancellation
dotnet run --project solution -- 4           # Exception handling
dotnet run --project solution -- 5           # ConfigureAwait
dotnet run --project solution -- 6           # Pitfalls
dotnet run --project solution -- 7           # Async data processor
dotnet run --project solution -- challenge   # Download manager
dotnet run --project solution -- all         # Everything in order
```

## Two Things That Will Bite You

### This project builds async warnings as errors

`Directory.Build.props` at the repository root promotes two warnings to errors:

| Code | Meaning |
|---|---|
| `CS4014` | You called an async method and did not await the task |
| `CS1998` | You marked a method `async` but never awaited anything in it |

Both are real bugs in almost every case, which is why the build stops on them.
When you hit one, read it rather than suppressing it. The reference solution
does suppress `CS4014` in exactly one place —
`solution/Examples/Pitfalls.cs`, where writing the anti-pattern is the point —
and the `#pragma` there is deliberately conspicuous.

### Parts 1, 2, 3 and 7 are slow on purpose

They use real `Task.Delay` calls, so Part 1 takes about eight seconds. That is
the demonstration: the synchronous half costs 6000ms and the asynchronous half
costs 2000ms for the same work.

The **tests**, by contrast, finish in well under a second. They use
`TaskCompletionSource` instead of delays, so ordering is exact rather than
probable. That contrast is worth internalizing: sleeping in a test makes it slow
*and* flaky, and there is nearly always a deterministic alternative.

## A Note on the Challenge

`EXERCISE.md` says to use `HttpClient`. The reference solution puts an
`IFileDownloader` interface in front of it, because a download manager wired
directly to `HttpClient` can only be tested with a network connection and real
waiting.

With the seam, `tests/DownloadManagerTests.cs` proves that retry stops at the
configured maximum, that a timeout is retried but a caller cancellation is not,
and that concurrency never exceeds its limit — offline, in milliseconds.

`solution/Challenge/SimulatedFileDownloader.cs` drives the demo, and
`HttpFileDownloader` in the same file is the real thing. Neither requires
`DownloadManager` to change, which is the module 01 lesson showing up again.

## Known Issue in EXERCISE.md

Part 5's `LibraryMethodExample` is written as `async Task` but returns a value.
It needs to be `async Task<string>` to compile. The reference solution has the
corrected version.

## If You Get Stuck

1. Read the compiler error — the async ones are unusually specific
2. Check you passed `CancellationToken` all the way down
3. Check you `await`ed everything you started
4. Compare against `solution/` — same namespaces and type names as the exercise
