# Exercise: Clean Architecture

## Overview

The demo in `src/` is complete and working. Your job is to **extend** it — and
to notice where the boundaries push back when you do.

Run it first:

```bash
dotnet run --project src/CleanArchitecture.WebApi
dotnet test tests/CleanArchitecture.Tests
```

---

## Part 1: Read the Dependency Rule

Before writing anything, convince yourself the rule holds.

1. Open `src/CleanArchitecture.Domain/CleanArchitecture.Domain.csproj`. It is
   empty. Why can that be?
2. Open `Application/Abstractions/Ports.cs`. `IProjectRepository` is *declared*
   here but *implemented* in Infrastructure. Which project references which?
3. Run `dotnet test --filter ArchitectureTests`. Now break it on purpose: add
   `<PackageReference Include="Microsoft.EntityFrameworkCore" />` to the Domain
   csproj and run the tests again.
4. Undo it.

**Question:** what would have caught that mistake if the test did not exist?

---

## Part 2: Add a Feature — Task Comments

Add the ability to comment on a task. Work outward, one layer at a time.

### 2.1 Domain

Add a `Comment` entity (`Id`, `TaskId`, `Author`, `Body`, `PostedAt`) and a
`TaskItem.AddComment(author, body, postedAt)` method. Enforce:

- author and body are required
- a comment cannot be added to a `Cancelled` task
- body is at most 1000 characters

Write the domain tests **first** — they need no database, so there is no excuse.

### 2.2 Application

Add `AddCommentUseCase` and `GetTaskCommentsUseCase`. You will need to decide
whether comments are loaded through the task or through a new port. Both are
defensible; write down why you chose yours.

### 2.3 Infrastructure

Map `Comment` in `AppDbContext`. Remember `.ValueGeneratedNever()` on the key,
and a converter for `PostedAt` if you order by it.

### 2.4 WebApi

`POST /tasks/{id}/comments` and `GET /tasks/{id}/comments`.

### 2.5 Reflect

Count the files you touched. Keep the number — Part 5 asks for it.

---

## Part 3: Swap an Implementation

The point of a port is that the adapter behind it can change.

1. Write `InMemoryProjectRepository` and `InMemoryTaskRepository` in
   Infrastructure, backed by dictionaries.
2. Add a configuration flag that picks in-memory or SQLite at startup
   (module 01, Part 5 — conditional registration).
3. Run the **integration** tests against both.

**Question:** which layers did you have to change? Which did not notice?

---

## Part 4: Add a Cross-Cutting Concern

Every use case should log how long it took, without any use case knowing about
logging.

Apply the decorator pattern from module 01, Part 2. You will find the use cases
have no common interface, which is the obstacle. Two ways out:

- give them one (`IUseCase<TIn, TOut>`) and decorate it
- or introduce MediatR and write a pipeline behaviour

Module 04 takes the second road. Try the first here, then compare.

---

## Part 5: Challenge — Make the Case

You now know this codebase and its counterpart in
[04-VerticalSliceArchitecture](../../04-VerticalSliceArchitecture/), which
implements the identical API.

Write a short document arguing which you would choose for:

1. A 6-person team building a payments system meant to last a decade
2. A 2-person startup validating an idea in 3 months
3. A 40-person team where each squad owns 2-3 features

Support it with numbers from the two codebases, not opinions:

- files touched to add one feature (you measured this in Part 2)
- total source lines and project count
- what the tests need in order to run
- where a business rule lives, and what stops a second caller from skipping it

There is no right answer. There is a badly-argued one.

---

## Reflection

1. Why does `Application` declare `IClock` instead of calling `DateTimeOffset.UtcNow`?
2. `CreateProjectUseCase` catches `DomainException` and returns `Result.Invalid`.
   Why not let the exception reach the API layer?
3. `AssignTaskUseCase` notifies **after** `SaveChangesAsync`. What breaks if you
   move it before?
4. Why does `CreateTaskUseCase` load the whole `Project` aggregate rather than
   just inserting a `TaskItem`?
5. `DomainTests` uses no mocking framework at all. What does that tell you about
   the design?
