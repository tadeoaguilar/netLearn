using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.UseCases.Projects;
using CleanArchitecture.Application.UseCases.Tasks;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Enums;

namespace CleanArchitecture.Tests;

/// <summary>
/// Use cases tested with no database, no web host and no container. Every
/// dependency is an interface the Application layer declared for itself.
/// </summary>
public class UseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeProjectRepository _projects = new();
    private readonly FakeTaskRepository _tasks = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new(Now);
    private readonly RecordingNotificationService _notifications = new();

    [Fact]
    public async Task Creating_a_project_persists_it_and_returns_a_dto()
    {
        var sut = new CreateProjectUseCase(_projects, _unitOfWork, _clock);

        var result = await sut.ExecuteAsync("Apollo");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Apollo");
        result.Value.CreatedAt.Should().Be(Now, "the use case takes time from IClock");
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Creating_a_project_without_a_name_is_a_validation_failure_not_an_exception()
    {
        var sut = new CreateProjectUseCase(_projects, _unitOfWork, _clock);

        var result = await sut.ExecuteAsync("   ");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ResultErrorKind.Validation);
        _unitOfWork.SaveCount.Should().Be(0, "nothing should be saved when the rule fails");
    }

    [Fact]
    public async Task Creating_a_task_on_a_missing_project_reports_NotFound()
    {
        var sut = new CreateTaskUseCase(_projects, _unitOfWork, _clock);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), "Write docs", null, Priority.Normal);

        result.Error!.Kind.Should().Be(ResultErrorKind.NotFound);
    }

    [Fact]
    public async Task Creating_a_task_goes_through_the_aggregate_root()
    {
        var project = Project.Create("Apollo", Now);
        _projects.Seed(project);
        var sut = new CreateTaskUseCase(_projects, _unitOfWork, _clock);

        var result = await sut.ExecuteAsync(project.Id, "Write docs", "details", Priority.High);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Priority.Should().Be("High");
        project.Tasks.Should().ContainSingle("the task was added to the aggregate, not created loose");
    }

    [Fact]
    public async Task Assigning_a_task_notifies_the_assignee()
    {
        var project = Project.Create("Apollo", Now);
        var task = project.AddTask("Write docs", null, Priority.Normal, Now);
        _tasks.Seed(task);
        var sut = new AssignTaskUseCase(_tasks, _unitOfWork, _clock, _notifications);

        var result = await sut.ExecuteAsync(task.Id, "tadeo");

        result.IsSuccess.Should().BeTrue();
        _notifications.Sent.Should().ContainSingle()
            .Which.Recipient.Should().Be("tadeo");
    }

    [Fact]
    public async Task A_failed_assignment_sends_no_notification()
    {
        // Ordering matters: notify only after the save succeeded, or you email
        // someone about an assignment that never happened.
        var project = Project.Create("Apollo", Now);
        var task = project.AddTask("Write docs", null, Priority.Normal, Now);
        task.Cancel();
        _tasks.Seed(task);
        var sut = new AssignTaskUseCase(_tasks, _unitOfWork, _clock, _notifications);

        var result = await sut.ExecuteAsync(task.Id, "tadeo");

        result.Error!.Kind.Should().Be(ResultErrorKind.Conflict);
        _notifications.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Completing_an_unassigned_task_is_a_conflict()
    {
        var project = Project.Create("Apollo", Now);
        var task = project.AddTask("Write docs", null, Priority.Normal, Now);
        _tasks.Seed(task);
        var sut = new CompleteTaskUseCase(_tasks, _unitOfWork, _clock);

        var result = await sut.ExecuteAsync(task.Id);

        result.Error!.Kind.Should().Be(ResultErrorKind.Conflict);
        result.Error.Message.Should().Contain("must be assigned");
    }

    [Fact]
    public async Task Completing_a_task_stamps_it_with_the_clocks_time()
    {
        var project = Project.Create("Apollo", Now);
        var task = project.AddTask("Write docs", null, Priority.Normal, Now);
        task.AssignTo("tadeo", Now);
        _tasks.Seed(task);
        _clock.UtcNow = Now.AddDays(2);
        var sut = new CompleteTaskUseCase(_tasks, _unitOfWork, _clock);

        var result = await sut.ExecuteAsync(task.Id);

        result.Value!.CompletedAt.Should().Be(Now.AddDays(2));
    }

    [Fact]
    public async Task Searching_filters_and_orders_by_priority()
    {
        var project = Project.Create("Apollo", Now);
        _tasks.Seed(
            project.AddTask("Low", null, Priority.Low, Now),
            project.AddTask("Urgent", null, Priority.Urgent, Now),
            project.AddTask("Normal", null, Priority.Normal, Now));
        var sut = new SearchTasksUseCase(_tasks);

        var results = await sut.ExecuteAsync(new TaskFilter(MinimumPriority: Priority.Normal));

        results.Select(t => t.Title).Should().Equal("Urgent", "Normal");
    }

    [Fact]
    public async Task Archiving_a_project_with_open_tasks_is_a_conflict()
    {
        var project = Project.Create("Apollo", Now);
        project.AddTask("Still open", null, Priority.Normal, Now);
        _projects.Seed(project);
        var sut = new ArchiveProjectUseCase(_projects, _unitOfWork);

        var result = await sut.ExecuteAsync(project.Id);

        result.Error!.Kind.Should().Be(ResultErrorKind.Conflict);
    }
}
