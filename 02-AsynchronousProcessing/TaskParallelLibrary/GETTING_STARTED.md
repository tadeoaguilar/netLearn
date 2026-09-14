# Getting Started with TaskParallelLibrary

## Quick Start

```bash
cd 02-AsynchronousProcessing/TaskParallelLibrary
dotnet build -c Release ../../netLearn.sln
```

## What Is Already Here

```
TaskParallelLibrary/
├── README.md                  # The concepts
├── EXERCISE.md                # The work, in 7 parts plus a challenge
├── GETTING_STARTED.md         # This file
│
├── TaskParallelLibrary/       # ← YOUR WORKSPACE. Write your code here.
│   ├── Program.cs
│   └── Examples/              #   empty, waiting for your files
│
├── solution/                  # ← REFERENCE IMPLEMENTATION.
│   ├── Examples/              #   Parts 1-7
│   ├── Challenge/             #   the parallel image processor
│   └── Demos/                 #   one runnable demo per part
│
└── tests/                     # ← 18 tests
```

## The Three Commands You Need

```bash
# Run your own work
dotnet run -c Release --project TaskParallelLibrary

# Check your work
dotnet test tests

# See the reference solution, one part at a time
dotnet run -c Release --project solution -- 1           # Parallel.For / ForEach
dotnet run -c Release --project solution -- 2           # PLINQ
dotnet run -c Release --project solution -- 3           # Degree of parallelism
dotnet run -c Release --project solution -- 4           # Exceptions
dotnet run -c Release --project solution -- 5           # Cancellation
dotnet run -c Release --project solution -- 6           # Partitioning
dotnet run -c Release --project solution -- 7           # Async vs parallel
dotnet run -c Release --project solution -- challenge   # Image processor
dotnet run -c Release --project solution -- all         # Everything
```

## Always Use `-c Release`

Every timing figure in this project is meaningless in a Debug build. Debug
disables most JIT optimization, and the sequential/parallel comparison you get
back will not resemble what your code does in production.

## Two Corrections to EXERCISE.md

**Part 1 computes speedup as `elapsed / elapsed`**, which is always `1.00x`. A
speedup figure needs both the sequential and the parallel measurement. The
reference solution's `BasicParallel.CompareBoth()` does it properly.

**Part 1 also calls `Console.Write` inside the parallel loop.** Console output
takes a process-wide lock, so that turns the parallel loop back into a serial
one and hides the very speedup it is trying to demonstrate. The reference
collects thread ids instead and prints after the loop.

Neither is a reason to skip the exercise — but if your numbers look wrong, these
are why.

## On Measuring

Your first parallel benchmark will probably disappoint you. Usual reasons:

1. **The workload is too small.** Starting threads costs more than the work.
2. **You are in Debug.** See above.
3. **You are printing inside the loop.** See above.
4. **The work is I/O, not CPU.** Parallelism is the wrong tool — see Part 7.
5. **The machine is busy.** Close other things and run it again.

This machine's core count is printed when you run the solution with no
arguments — the ceiling on any speedup you can get.

## A Note on the Challenge

`EXERCISE.md` asks for "100 simulated images". The reference puts an
`IImageOperation` interface in front of the per-image work, so the tests can
make an image fail, hang, or take a known amount of time, without waiting on
real CPU-bound work.

`SimulatedResizeOperation` does genuine arithmetic rather than `Thread.Sleep` —
sleeping would make it fake *I/O*, and the whole point of the challenge is that
this is CPU-bound work.

## If You Get Stuck

1. Are you mutating shared state without `Interlocked` or a concurrent collection?
2. Are you using `List<T>` where multiple threads write? Use `ConcurrentBag<T>`.
3. Is your exception an `AggregateException` hiding the real one? Check `.InnerExceptions`.
4. Compare against `solution/` — same namespaces and type names as the exercise.
