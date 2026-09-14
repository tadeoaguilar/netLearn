using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Events;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Domain.ValueObjects;

namespace TaskManagement.Tests;

/// <summary>
/// No database, no host, no mocks. Being able to test the rules this way is the
/// return on the four-project structure.
/// </summary>
public class DomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Project NewProject() => Project.Create("Apollo", "moon shot", "alice");

    [Fact]
    public void A_new_project_is_active_and_owned_by_its_creator()
    {
        var project = NewProject();

        project.Status.Should().Be(ProjectStatus.Active);
        project.OwnerId.Should().Be("alice");
        project.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProjectCreatedEvent>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_project_requires_a_name(string? name)
    {
        var act = () => Project.Create(name!, null, "alice");

        act.Should().Throw<DomainException>().WithMessage("*Title is required*");
    }

    [Fact]
    public void A_title_is_trimmed_and_length_limited()
    {
        Title.Create("  spaced  ").Value.Should().Be("spaced");

        FluentActions.Invoking(() => Title.Create(new string('x', Title.MaxLength + 1)))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void A_new_task_starts_in_Todo()
    {
        var task = NewProject().AddTask("Write docs", null, TaskPriority.Normal, null);

        task.State.Should().Be(TaskState.Todo);
        task.AssigneeId.Should().BeNull();
        task.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Assigning_a_Todo_task_moves_it_to_InProgress()
    {
        var task = NewProject().AddTask("Write docs", null, TaskPriority.Normal, null);

        task.AssignTo("bob");

        task.State.Should().Be(TaskState.InProgress);
        task.AssigneeId.Should().Be("bob");
        task.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TaskAssignedEvent>();
    }

    [Fact]
    public void A_task_cannot_be_completed_before_it_is_assigned()
    {
        var task = NewProject().AddTask("Write docs", null, TaskPriority.Normal, null);

        var act = () => task.Complete(Now);

        act.Should().Throw<DomainException>().WithMessage("*must be assigned*");
    }

    [Theory]
    [InlineData(TaskState.Todo, TaskState.Done)]        // skips InProgress and InReview
    [InlineData(TaskState.Todo, TaskState.InReview)]    // skips InProgress
    public void Illegal_transitions_are_refused(TaskState from, TaskState to)
    {
        // The legal moves live in one table on the entity rather than scattered
        // across handlers, so every caller gets them.
        var task = NewProject().AddTask("Write docs", null, TaskPriority.Normal, null);
        task.MoveTo(from, Now);

        var act = () => task.MoveTo(to, Now);

        act.Should().Throw<DomainException>().WithMessage($"*from {from} to {to}*");
    }

    [Fact]
    public void The_legal_path_to_Done_runs_end_to_end()
    {
        var task = NewProject().AddTask("Write docs", null, TaskPriority.Normal, null);

        task.AssignTo("bob");                      // Todo -> InProgress
        task.MoveTo(TaskState.InReview, Now);      // InProgress -> InReview
        task.MoveTo(TaskState.Done, Now);          // InReview -> Done

        task.State.Should().Be(TaskState.Done);
        task.CompletedAt.Should().Be(Now);
        task.DomainEvents.Should().Contain(e => e is TaskCompletedEvent);
    }

    [Fact]
    public void A_done_task_is_terminal()
    {
        var task = NewProject().AddTask("Write docs", null, TaskPriority.Normal, null);
        task.AssignTo("bob");
        task.MoveTo(TaskState.InReview, Now);
        task.MoveTo(TaskState.Done, Now);

        FluentActions.Invoking(() => task.MoveTo(TaskState.InProgress, Now)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => task.Cancel()).Should().Throw<DomainException>();
        FluentActions.Invoking(() => task.AssignTo("carol")).Should().Throw<DomainException>();
    }

    [Fact]
    public void Overdue_is_computed_against_the_supplied_time()
    {
        var due = Now.AddDays(-1);
        var task = NewProject().AddTask("Late", null, TaskPriority.Normal, due);

        task.IsOverdue(Now).Should().BeTrue();
        task.IsOverdue(due.AddHours(-1)).Should().BeFalse();
    }

    [Fact]
    public void A_completed_task_is_never_overdue()
    {
        var task = NewProject().AddTask("Late but done", null, TaskPriority.Normal, Now.AddDays(-1));
        task.AssignTo("bob");
        task.MoveTo(TaskState.InReview, Now);
        task.MoveTo(TaskState.Done, Now);

        task.IsOverdue(Now).Should().BeFalse();
    }

    [Fact]
    public void An_aggregate_enforces_the_open_task_cap()
    {
        var project = NewProject();

        for (var i = 0; i < Project.MaxOpenTasks; i++)
        {
            project.AddTask($"Task {i}", null, TaskPriority.Normal, null);
        }

        var act = () => project.AddTask("One too many", null, TaskPriority.Normal, null);

        act.Should().Throw<DomainException>().WithMessage($"*{Project.MaxOpenTasks} open tasks*");
    }

    [Fact]
    public void A_project_with_open_tasks_cannot_be_archived()
    {
        var project = NewProject();
        project.AddTask("Still open", null, TaskPriority.Normal, null);

        var act = () => project.Archive();

        act.Should().Throw<DomainException>().WithMessage("*1 open task*");
    }

    [Fact]
    public void An_archived_project_is_frozen()
    {
        var project = NewProject();
        project.Archive();

        FluentActions.Invoking(() => project.AddTask("Too late", null, TaskPriority.Normal, null))
            .Should().Throw<DomainException>();
        FluentActions.Invoking(() => project.Rename("New name"))
            .Should().Throw<DomainException>();
    }
}
