using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Enums;
using CleanArchitecture.Domain.Events;
using CleanArchitecture.Domain.Exceptions;
using CleanArchitecture.Domain.ValueObjects;

namespace CleanArchitecture.Tests;

/// <summary>
/// Domain tests need no database, no container, no mocks -- just objects.
/// That they can be written this way is the payoff of the dependency rule.
/// </summary>
public class DomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Project NewProject() => Project.Create("Apollo", Now);

    [Fact]
    public void A_task_starts_in_Todo_with_no_assignee()
    {
        var task = NewProject().AddTask("Write docs", null, Priority.Normal, Now);

        task.State.Should().Be(TaskState.Todo);
        task.Assignee.Should().BeNull();
        task.CompletedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_task_cannot_be_created_without_a_title(string? title)
    {
        var project = NewProject();

        var act = () => project.AddTask(title!, null, Priority.Normal, Now);

        act.Should().Throw<DomainException>().WithMessage("*title is required*");
    }

    [Fact]
    public void A_title_longer_than_the_limit_is_rejected()
    {
        var act = () => TaskTitle.Create(new string('x', TaskTitle.MaxLength + 1));

        act.Should().Throw<DomainException>().WithMessage($"*{TaskTitle.MaxLength}*");
    }

    [Fact]
    public void A_title_is_trimmed()
    {
        TaskTitle.Create("  spaced  ").Value.Should().Be("spaced");
    }

    [Fact]
    public void Assigning_a_Todo_task_moves_it_to_InProgress()
    {
        var task = NewProject().AddTask("Write docs", null, Priority.Normal, Now);

        task.AssignTo("tadeo", Now);

        task.State.Should().Be(TaskState.InProgress);
        task.Assignee.Should().Be("tadeo");
    }

    [Fact]
    public void A_task_cannot_be_completed_before_it_is_assigned()
    {
        // The rule that makes this a domain entity rather than a data bag.
        var task = NewProject().AddTask("Write docs", null, Priority.Normal, Now);

        var act = () => task.Complete(Now);

        act.Should().Throw<DomainException>().WithMessage("*must be assigned*");
    }

    [Fact]
    public void Completing_a_task_records_when_it_happened()
    {
        var task = NewProject().AddTask("Write docs", null, Priority.Normal, Now);
        task.AssignTo("tadeo", Now);

        var completedAt = Now.AddHours(3);
        task.Complete(completedAt);

        task.State.Should().Be(TaskState.Done);
        task.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void A_completed_task_cannot_be_completed_again()
    {
        var task = NewProject().AddTask("Write docs", null, Priority.Normal, Now);
        task.AssignTo("tadeo", Now);
        task.Complete(Now);

        var act = () => task.Complete(Now);

        act.Should().Throw<DomainException>().WithMessage("*already complete*");
    }

    [Fact]
    public void A_cancelled_task_cannot_be_assigned_or_completed()
    {
        var task = NewProject().AddTask("Write docs", null, Priority.Normal, Now);
        task.Cancel();

        FluentActions.Invoking(() => task.AssignTo("tadeo", Now))
            .Should().Throw<DomainException>();
        FluentActions.Invoking(() => task.Complete(Now))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Assigning_and_completing_record_domain_events()
    {
        var task = NewProject().AddTask("Write docs", null, Priority.Normal, Now);

        task.AssignTo("tadeo", Now);
        task.Complete(Now.AddHours(1));

        task.DomainEvents.Should().HaveCount(2);
        task.DomainEvents[0].Should().BeOfType<TaskAssignedEvent>();
        task.DomainEvents[1].Should().BeOfType<TaskCompletedEvent>()
            .Which.Assignee.Should().Be("tadeo");
    }

    [Fact]
    public void An_aggregate_enforces_a_rule_no_single_task_could()
    {
        // The open-task cap spans the whole collection, which is precisely why
        // it lives on the aggregate root.
        var project = NewProject();

        for (var i = 0; i < Project.MaxOpenTasks; i++)
        {
            project.AddTask($"Task {i}", null, Priority.Normal, Now);
        }

        var act = () => project.AddTask("One too many", null, Priority.Normal, Now);

        act.Should().Throw<DomainException>().WithMessage($"*{Project.MaxOpenTasks} open tasks*");
    }

    [Fact]
    public void Completing_tasks_frees_capacity_again()
    {
        var project = NewProject();

        for (var i = 0; i < Project.MaxOpenTasks; i++)
        {
            var task = project.AddTask($"Task {i}", null, Priority.Normal, Now);
            task.AssignTo("tadeo", Now);
            task.Complete(Now);
        }

        var act = () => project.AddTask("Now there is room", null, Priority.Normal, Now);

        act.Should().NotThrow();
    }

    [Fact]
    public void A_project_with_open_tasks_cannot_be_archived()
    {
        var project = NewProject();
        project.AddTask("Still open", null, Priority.Normal, Now);

        var act = () => project.Archive();

        act.Should().Throw<DomainException>().WithMessage("*open tasks*");
    }

    [Fact]
    public void An_archived_project_accepts_no_new_tasks()
    {
        var project = NewProject();
        project.Archive();

        var act = () => project.AddTask("Too late", null, Priority.Normal, Now);

        act.Should().Throw<DomainException>().WithMessage("*archived*");
    }
}
