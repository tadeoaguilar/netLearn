# Getting Started with VerticalSliceDemo

## Quick Start

```bash
cd 04-VerticalSliceArchitecture/VerticalSliceDemo
dotnet build
dotnet test tests/VerticalSlice.Tests
dotnet run --project src/VerticalSlice.Api
```

## This Is the Same API as Module 03

That is the point. `03-CleanArchitecture/CleanArchitectureDemo` and this project
expose an **identical HTTP contract** — same routes, same status codes, same
JSON. The integration tests in both projects assert the same behaviour.

Only the internal organisation differs. You can therefore compare the two
honestly, because the thing being built is held constant.

## Layout

```
VerticalSliceDemo/
└── src/VerticalSlice.Api/          # ONE project, not four
    ├── Common/                     # genuinely shared plumbing
    │   ├── Database/AppDbContext.cs
    │   ├── Behaviors/ValidationBehavior.cs
    │   ├── Domain/Entities.cs
    │   ├── Results.cs
    │   └── IEndpoint.cs
    └── Features/
        ├── Projects/
        │   ├── CreateProject/CreateProject.cs      ← command + validator
        │   ├── ListProjects/ListProjects.cs        ←   + handler + endpoint
        │   └── ArchiveProject/ArchiveProject.cs    ←   all in one file
        └── Tasks/
            ├── CreateTask/CreateTask.cs
            ├── AssignTask/AssignTask.cs
            ├── CompleteTask/CompleteTask.cs
            └── SearchTasks/SearchTasks.cs
```

Open `Features/Projects/CreateProject/CreateProject.cs`. The command, its
validator, the handler and the HTTP endpoint are all there, in that order, in
about 70 lines. Everything that feature does, you can read without scrolling
past anything that feature does not do.

## The Comparison, Measured

Both codebases implement the same seven endpoints.

| | Clean Architecture (03) | Vertical Slice (04) |
|---|---|---|
| Projects | 4 | 1 |
| Source files | 32 | 16 |
| Source lines | ~1,290 | ~859 |
| Files touched to add "create a project" | **5**, across 3 projects | **1** |
| Where a business rule lives | on the entity | in the handler that needs it |
| What a unit test needs | nothing (domain) | a `DbContext` |
| What stops a second caller skipping a rule | the entity does | nothing — you must remember |

Those last two rows are the trade, stated plainly.

**Module 03** puts "a task must be assigned before completion" on
`TaskItem.Complete()`. Every caller gets it, forever, whether they thought
about it or not.

**Module 04** puts the same rule in `CompleteTaskHandler`. It is right there
next to the code that needed it — and a second feature that completes tasks
would have to repeat it, or silently skip it.

Neither is a mistake. They optimise for different risks.

## Where the Slices Win

**Adding a feature touches one folder.** No jumping between four projects, no
editing a shared `Program.cs`, no merge conflict with the person adding the
feature next to yours. `Common/IEndpoint.cs` discovers routes by reflection, so
even route registration is local to the slice.

**Each slice can make its own call.** `CreateTaskHandler` counts open tasks with
`CountAsync` instead of loading the aggregate — cheaper, and available because
no shared repository interface had to agree. Module 03 loads the whole `Project`
to enforce the same rule, which is correct but heavier, and no single use case
is free to change it.

**Less indirection.** A handler talks to `DbContext`. There is no repository
wrapping a `DbSet` that already is one, and no port to define when only one
feature will ever use it.

## Where the Layers Win

**Rules are enforced, not remembered.** See above.

**Tests need less.** `03`'s `DomainTests` runs against plain objects. `04`'s
handler tests need a SQLite connection, because the handler talks to EF Core
directly. Fast, but not free, and the test has to know about persistence.

**The boundary is checkable.** `03` has `ArchitectureTests` that fail the build
if `Domain` ever references EF Core. There is no equivalent here — the
discipline lives in review.

## The Pipeline Behaviour

`Common/Behaviors/ValidationBehavior.cs` is the decorator pattern from module 01
applied to every request at once. Register it once and no handler ever validates
its own input again.

This is the piece `04` gets for free by adopting MediatR, and the thing
`03/EXERCISE.md` Part 4 asks you to build by hand — so you can feel the
difference.

## Try It

```bash
dotnet run --project src/VerticalSlice.Api
curl http://localhost:5000/
```

Then run the *same* curl commands from
`03-CleanArchitecture/CleanArchitectureDemo/GETTING_STARTED.md` against this
API. Every one of them behaves identically.

## If You Get Stuck

- **Your new endpoint 404s** — did the class implement `IEndpoint`? Discovery is
  by reflection; a slice that forgets it simply never registers.
- **Your validator never runs** — `AddValidatorsFromAssembly` finds
  `AbstractValidator<TCommand>` where `TCommand` is the exact request type.
- **MediatR cannot find a handler** — check `IRequest<TResponse>` and
  `IRequestHandler<TRequest, TResponse>` agree on `TResponse`.
