using FluentValidation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using VerticalSlice.Api.Common.Database;
using VerticalSlice.Api.Common.Domain;
using VerticalSlice.Api.Features.Projects.ArchiveProject;
using VerticalSlice.Api.Features.Projects.CreateProject;
using VerticalSlice.Api.Features.Tasks.AssignTask;
using VerticalSlice.Api.Features.Tasks.CompleteTask;
using VerticalSlice.Api.Features.Tasks.CreateTask;
using VerticalSlice.Api.Features.Tasks.SearchTasks;

namespace VerticalSlice.Tests;

/// <summary>
/// A handler test needs a DbContext, because the handler talks to one directly.
///
/// That is the honest trade against module 03, where a use case takes an
/// interface and needs no database at all. SQLite in-memory keeps it fast, but
/// notice that the test has to know about persistence either way.
/// </summary>
public abstract class SliceTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    protected readonly AppDbContext Context;
    protected readonly FakeTimeProvider Time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    protected SliceTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);

        Context.Database.EnsureCreated();
    }

    protected async Task<Project> SeedProjectAsync(string name = "Apollo")
    {
        var project = new Project { Name = name, CreatedAt = Time.GetUtcNow() };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        return project;
    }

    protected async Task<TaskItem> SeedTaskAsync(
        Guid projectId, string title = "Write docs", Priority priority = Priority.Normal)
    {
        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = title,
            Priority = priority,
            State = TaskState.Todo,
            CreatedAt = Time.GetUtcNow()
        };
        Context.Tasks.Add(task);
        await Context.SaveChangesAsync();
        return task;
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class CreateProjectTests : SliceTestBase
{
    [Fact]
    public async Task Creates_a_project_with_the_provided_clock_time()
    {
        var sut = new CreateProjectHandler(Context, Time);

        var result = await sut.Handle(new CreateProjectCommand("  Apollo  "), default);

        result.Name.Should().Be("Apollo", "the handler trims");
        result.CreatedAt.Should().Be(Time.GetUtcNow());
        (await Context.Projects.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task The_validator_rejects_a_blank_name(string name)
    {
        // Validation lives in the slice's validator, not the handler, so this
        // is where it is tested.
        var validator = new CreateProjectValidator();

        var result = await validator.ValidateAsync(new CreateProjectCommand(name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Project name is required.");
    }

    [Fact]
    public async Task The_validator_rejects_an_over_long_name()
    {
        var validator = new CreateProjectValidator();

        var result = await validator.ValidateAsync(new CreateProjectCommand(new string('x', 201)));

        result.IsValid.Should().BeFalse();
    }
}

public class CreateTaskTests : SliceTestBase
{
    [Fact]
    public async Task Creates_a_task_in_Todo()
    {
        var project = await SeedProjectAsync();
        var sut = new CreateTaskHandler(Context, Time);

        var result = await sut.Handle(
            new CreateTaskCommand(project.Id, "Write docs", null, Priority.High), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.State.Should().Be("Todo");
        result.Value.Priority.Should().Be("High");
    }

    [Fact]
    public async Task Reports_NotFound_for_a_missing_project()
    {
        var sut = new CreateTaskHandler(Context, Time);

        var result = await sut.Handle(
            new CreateTaskCommand(Guid.NewGuid(), "Write docs", null), default);

        result.Error!.Kind.Should().Be(VerticalSlice.Api.Common.ResultErrorKind.NotFound);
    }

    [Fact]
    public async Task Refuses_to_add_a_task_to_an_archived_project()
    {
        var project = await SeedProjectAsync();
        project.IsArchived = true;
        await Context.SaveChangesAsync();
        var sut = new CreateTaskHandler(Context, Time);

        var result = await sut.Handle(new CreateTaskCommand(project.Id, "Too late", null), default);

        result.Error!.Kind.Should().Be(VerticalSlice.Api.Common.ResultErrorKind.Conflict);
    }

    [Fact]
    public async Task Enforces_the_open_task_cap()
    {
        var project = await SeedProjectAsync();
        for (var i = 0; i < CreateTaskHandler.MaxOpenTasks; i++)
        {
            await SeedTaskAsync(project.Id, $"Task {i}");
        }
        var sut = new CreateTaskHandler(Context, Time);

        var result = await sut.Handle(new CreateTaskCommand(project.Id, "One too many", null), default);

        result.Error!.Message.Should().Contain($"{CreateTaskHandler.MaxOpenTasks} open tasks");
    }
}

public class AssignAndCompleteTests : SliceTestBase
{
    private sealed class RecordingNotifier : ITaskNotifier
    {
        public List<string> Notified { get; } = new();

        public Task NotifyAssignedAsync(string assignee, string taskTitle, CancellationToken cancellationToken)
        {
            Notified.Add(assignee);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Assigning_moves_Todo_to_InProgress_and_notifies()
    {
        var project = await SeedProjectAsync();
        var task = await SeedTaskAsync(project.Id);
        var notifier = new RecordingNotifier();
        var sut = new AssignTaskHandler(Context, notifier);

        var result = await sut.Handle(new AssignTaskCommand(task.Id, "tadeo"), default);

        result.Value!.State.Should().Be("InProgress");
        notifier.Notified.Should().ContainSingle().Which.Should().Be("tadeo");
    }

    [Fact]
    public async Task A_rejected_assignment_notifies_nobody()
    {
        var project = await SeedProjectAsync();
        var task = await SeedTaskAsync(project.Id);
        task.State = TaskState.Cancelled;
        await Context.SaveChangesAsync();
        var notifier = new RecordingNotifier();
        var sut = new AssignTaskHandler(Context, notifier);

        var result = await sut.Handle(new AssignTaskCommand(task.Id, "tadeo"), default);

        result.IsSuccess.Should().BeFalse();
        notifier.Notified.Should().BeEmpty();
    }

    [Fact]
    public async Task A_task_cannot_be_completed_before_it_is_assigned()
    {
        var project = await SeedProjectAsync();
        var task = await SeedTaskAsync(project.Id);
        var sut = new CompleteTaskHandler(Context, Time);

        var result = await sut.Handle(new CompleteTaskCommand(task.Id), default);

        result.Error!.Message.Should().Contain("must be assigned");
    }

    [Fact]
    public async Task Completing_stamps_the_clock_time()
    {
        var project = await SeedProjectAsync();
        var task = await SeedTaskAsync(project.Id);
        task.Assignee = "tadeo";
        task.State = TaskState.InProgress;
        await Context.SaveChangesAsync();

        Time.Advance(TimeSpan.FromHours(5));
        var sut = new CompleteTaskHandler(Context, Time);

        var result = await sut.Handle(new CompleteTaskCommand(task.Id), default);

        result.Value!.CompletedAt.Should().Be(Time.GetUtcNow());
    }
}

public class SearchAndArchiveTests : SliceTestBase
{
    [Fact]
    public async Task Search_orders_by_priority_rank_not_spelling()
    {
        var project = await SeedProjectAsync();
        await SeedTaskAsync(project.Id, "low", Priority.Low);
        await SeedTaskAsync(project.Id, "urgent", Priority.Urgent);
        await SeedTaskAsync(project.Id, "high", Priority.High);
        var sut = new SearchTasksHandler(Context);

        var results = await sut.Handle(new SearchTasksQuery(project.Id), default);

        results.Select(t => t.Priority).Should().Equal("Urgent", "High", "Low");
    }

    [Fact]
    public async Task Search_filters_by_minimum_priority()
    {
        var project = await SeedProjectAsync();
        await SeedTaskAsync(project.Id, "low", Priority.Low);
        await SeedTaskAsync(project.Id, "high", Priority.High);
        var sut = new SearchTasksHandler(Context);

        var results = await sut.Handle(
            new SearchTasksQuery(project.Id, MinimumPriority: Priority.High), default);

        results.Should().ContainSingle().Which.Title.Should().Be("high");
    }

    [Fact]
    public async Task A_project_with_open_tasks_cannot_be_archived()
    {
        var project = await SeedProjectAsync();
        await SeedTaskAsync(project.Id);
        var sut = new ArchiveProjectHandler(Context);

        var result = await sut.Handle(new ArchiveProjectCommand(project.Id), default);

        result.Error!.Message.Should().Contain("open tasks");
    }

    [Fact]
    public async Task A_project_with_no_open_tasks_archives()
    {
        var project = await SeedProjectAsync();
        var sut = new ArchiveProjectHandler(Context);

        var result = await sut.Handle(new ArchiveProjectCommand(project.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsArchived.Should().BeTrue();
    }
}
