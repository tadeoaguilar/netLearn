# Getting Started with CleanArchitectureDemo

## Quick Start

```bash
cd 03-CleanArchitecture/CleanArchitectureDemo
dotnet build
dotnet test tests/CleanArchitecture.Tests
dotnet run --project src/CleanArchitecture.WebApi
```

The API starts on the port Kestrel prints. `GET /` lists every endpoint.

## Layout

```
CleanArchitectureDemo/
├── src/
│   ├── CleanArchitecture.Domain/          # entities, value objects, events. NO dependencies.
│   ├── CleanArchitecture.Application/     # use cases + the PORTS they need
│   ├── CleanArchitecture.Infrastructure/  # EF Core, notifications: the ADAPTERS
│   └── CleanArchitecture.WebApi/          # endpoints + composition root
└── tests/
    └── CleanArchitecture.Tests/           # 41 tests: domain, use case, architecture, integration
```

Dependencies point inward only:

```
WebApi ──→ Infrastructure ──→ Application ──→ Domain
   └──────────────────────────────┘
```

`CleanArchitecture.Domain.csproj` is deliberately empty — no `PackageReference`,
no `ProjectReference`. That emptiness *is* the architecture.

## A Note on This Module's Shape

Modules 01 and 02 give you a workspace to fill in, a `solution/` to compare
against, and tests. From module 03 on, the shape changes: the demo **is** the
reference implementation, and `EXERCISE.md` asks you to extend it rather than
rebuild it from scratch.

That is deliberate. Typing out a four-layer solution from a tutorial teaches
you very little; changing one and feeling where the boundaries push back
teaches you a lot.

## Try It

```bash
dotnet run --project src/CleanArchitecture.WebApi
```

```bash
# Create a project
curl -X POST http://localhost:5000/projects \
  -H 'Content-Type: application/json' -d '{"name":"Apollo"}'

# Add a task (use the id from above)
curl -X POST http://localhost:5000/projects/$ID/tasks \
  -H 'Content-Type: application/json' \
  -d '{"title":"Write the docs","priority":"High"}'

# Completing before assigning is refused by the DOMAIN, and surfaces as 409
curl -X POST http://localhost:5000/tasks/$TASK/complete

# Assign, then complete
curl -X POST http://localhost:5000/tasks/$TASK/assign \
  -H 'Content-Type: application/json' -d '{"assignee":"you"}'
curl -X POST http://localhost:5000/tasks/$TASK/complete

# Search
curl "http://localhost:5000/tasks?state=Done&minimumPriority=High"
```

Watch the console: assigning prints a `[notify]` line and an `[event]` line.
The notification comes from an Infrastructure adapter; the event was recorded
by the entity and dispatched by the unit of work after the save committed.

## The Tests Are the Point

| File | What it proves | Needs |
|---|---|---|
| `DomainTests.cs` | Business rules | nothing — just objects |
| `UseCaseTests.cs` | Orchestration | hand-written fakes |
| `ArchitectureTests.cs` | The dependency rule itself | reflection over assemblies |
| `IntegrationTests.cs` | The whole stack | real HTTP + real SQLite |

`DomainTests` needs no container, no database, and no mocking framework. Being
able to write it that way is the entire return on the extra structure.

`ArchitectureTests` is the unusual one: it reads the compiled assemblies and
fails if `Domain` ever references EF Core, or if `Application` ever references
`Infrastructure`. An architecture that is only written down in a README is a
convention until the first deadline.

## Three Bugs This Module's Tests Caught

All three were found by running the code, not by reading it. They are left
documented in the source because each is a trap you will hit again:

**1. `DbUpdateConcurrencyException` on the very first insert.**
`Entity` assigns `Guid.NewGuid()` in a field initializer, so a new task already
has a key. EF assumes a set key means an existing row and issues an `UPDATE`
that matches nothing. The fix is `.ValueGeneratedNever()` — see
`AppDbContext.cs`. Identity belongs to the domain here, and EF has to be told.

**2. SQLite refuses to `ORDER BY` a `DateTimeOffset`.**
`"SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY
clauses"`. Fixed with a value converter storing UTC ticks. Note where the fix
lives: Infrastructure. `TaskItem.CreatedAt` is still a `DateTimeOffset`, and the
domain never learns that SQLite has opinions.

**3. Priority sorted alphabetically.**
Storing the enum as text made `ORDER BY` lexicographic, so `High` came out
below `Low`. Test data with only Low/Normal/Urgent hid it — those three happen
to sort correctly. `Priority` is now stored as its ordinal, and
`IntegrationTests` has a regression test with all four values.

That third one is the argument for integration tests over in-memory ones: EF
Core's InMemory provider would have passed all three.

## If You Get Stuck

- **A new dependency won't resolve** — it probably needs registering in the
  layer's own `DependencyInjection.cs`, not in `Program.cs`.
- **`ArchitectureTests` fails** — you added a reference pointing outward. That
  is the test doing its job.
- **EF says a property has no setter** — entities use private setters on
  purpose. Map to the backing field, as `AppDbContext` does for `Project.Tasks`.
