# Exercise: Vertical Slice Architecture

## Overview

The demo in `src/` implements the same seven endpoints as
[03-CleanArchitecture](../../03-CleanArchitecture/), organised by feature
instead of by layer. Your job is to extend it, then judge the two fairly.

```bash
dotnet run --project src/VerticalSlice.Api
dotnet test tests/VerticalSlice.Tests
```

---

## Part 1: Read One Slice

Open `Features/Projects/CreateProject/CreateProject.cs`.

1. Find the command, the validator, the handler and the endpoint. All four are
   in that one file.
2. Now open the module 03 equivalent. Creating a project there lives in
   `Domain/Entities/Project.cs`, `Application/UseCases/Projects/ProjectUseCases.cs`,
   `Application/DependencyInjection.cs`, `WebApi/Contracts.cs` and
   `WebApi/Program.cs`.
3. Ask yourself honestly which you would rather open at 5pm on a Friday — and
   then which you would rather inherit in two years.

---

## Part 2: Add a Feature — Task Comments

Add commenting, exactly as module 03's exercise does, so you can compare the
effort directly.

Create `Features/Tasks/AddComment/AddComment.cs` containing:

- `AddCommentCommand(Guid TaskId, string Author, string Body)`
- `AddCommentValidator` — author and body required, body ≤ 1000 characters
- `AddCommentHandler` — reject comments on a `Cancelled` task
- `AddCommentEndpoint` — `POST /tasks/{id}/comments`

Then `Features/Tasks/GetComments/GetComments.cs` for the read side.

You will need a `Comment` entity in `Common/Domain/Entities.cs` and a mapping in
`AppDbContext` — that part is shared, and that is a real cost worth noticing.

**Count the files you touched.** Compare with the count from module 03's Part 2.

---

## Part 3: Feel the Trade-off

Add a second way to complete a task: `POST /projects/{id}/complete-all`, which
completes every assigned task in a project.

Write the handler **without** looking at `CompleteTaskHandler`.

Now compare the two. Did you remember that a task must be assigned before it can
be completed? Did you remember that a cancelled task cannot be completed?

In module 03, `TaskItem.Complete()` would have enforced both whether you
remembered or not.

This is the single most important exercise in the module. Do not skip it.

---

## Part 4: Add a Pipeline Behaviour

`ValidationBehavior` already runs for every request. Add a second one,
`LoggingBehavior<TRequest, TResponse>`, that logs each request's name and
elapsed time.

Register it and confirm it wraps every slice with no slice changing.

**Question:** module 03's exercise asks you to achieve this with decorators and
no MediatR. Which was less work? Which would you rather debug a stack trace
through?

---

## Part 5: Extract a Slice

Vertical slice is often justified as "easy to extract into a microservice
later". Test that claim.

Move the `Features/Tasks/` slices into a new project that talks to the same
database. Note what breaks: `Common/Domain/Entities.cs`, `AppDbContext`,
`Common/Results.cs` are all shared.

**Question:** was it as easy as advertised? What would you have had to do
differently from day one to make it genuinely easy?

---

## Part 6: Challenge — Write the Recommendation

Both codebases are in front of you, implementing the identical API.

Produce a one-page recommendation for a team about to start a new system.
Use measurements, not preferences:

| Question | How to answer it |
|---|---|
| Cost to add a feature | files touched in Part 2, both modules |
| Cost to change a rule everywhere | try changing the open-task cap in both |
| Cost to onboard | time yourself finding where "complete a task" happens |
| Cost of a mistake | what Part 3 showed you |
| Cost to test | what each test class needs before it can run |

State the conditions under which you would switch your answer.

---

## Reflection

1. `SearchTasksHandler` queries `DbContext` directly. What would a repository
   interface have bought here, and what would it have cost?
2. `Common/` holds the `DbContext`, entities and result types. What is your test
   for whether something belongs in `Common/` rather than a slice?
3. `AssignTask` declares its own `ITaskNotifier`, used by one slice. Is a port
   used by exactly one caller still worth having?
4. Module 03 cannot accidentally reference Infrastructure from Application — a
   test enforces it. What enforces anything here?
5. If this codebase grew to 200 slices, what would you expect to go wrong first?
