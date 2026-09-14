using FluentValidation;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskManagement.Application;
using TaskManagement.Application.Common.Interfaces;
using TaskManagement.Application.Projects.Commands;
using TaskManagement.Application.Projects.Queries;
using TaskManagement.Application.Tasks.Commands;
using TaskManagement.Application.Tasks.Queries;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Exceptions;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Tests;

/// <summary>A caller the test controls completely.</summary>
public class TestUser : ICurrentUser
{
    public string? UserId { get; set; } = "alice";
    public string? UserName { get; set; } = "alice";
    public bool IsAuthenticated => UserId is not null;
    public HashSet<string> Roles { get; } = new();
    public bool IsInRole(string role) => Roles.Contains(role);
}

public class FixedClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

/// <summary>
/// Handlers exercised through MediatR, so the validation and logging behaviours
/// run exactly as they do in production.
/// </summary>
public class ApplicationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _provider;
    private readonly TestUser _user = new();
    private readonly FixedClock _clock = new();

    public ApplicationTests()
    {
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddApplication();
        services.AddSingleton<ICurrentUser>(_user);
        services.AddSingleton<IDateTimeProvider>(_clock);

        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        _provider = services.BuildServiceProvider();
        _provider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
    }

    private ISender Sender => _provider.GetRequiredService<ISender>();

    [Fact]
    public async Task Creating_a_project_takes_ownership_from_the_caller_not_the_request()
    {
        // A request body carrying an owner id would let anyone create projects
        // owned by anyone.
        _user.UserId = "bob";

        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));

        project.OwnerId.Should().Be("bob");
        project.CreatedAt.Should().Be(_clock.UtcNow, "the audit stamp comes from IDateTimeProvider");
    }

    [Fact]
    public async Task The_validation_behaviour_rejects_before_the_handler_runs()
    {
        var act = async () => await Sender.Send(new CreateProjectCommand("", null));

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task A_non_owner_without_the_admin_role_is_refused()
    {
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));

        _user.UserId = "mallory";

        var act = async () => await Sender.Send(
            new UpdateProjectCommand(project.Id, "Hijacked", null, ProjectStatus.Active));

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task An_admin_may_modify_any_project()
    {
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));

        _user.UserId = "carol";
        _user.Roles.Add("admin");

        var updated = await Sender.Send(
            new UpdateProjectCommand(project.Id, "Renamed by admin", null, ProjectStatus.Active));

        updated.Name.Should().Be("Renamed by admin");
    }

    [Fact]
    public async Task A_missing_project_reports_NotFound()
    {
        var act = async () => await Sender.Send(new GetProjectQuery(Guid.NewGuid()));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Creating_a_task_goes_through_the_aggregate_root()
    {
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));

        var task = await Sender.Send(new CreateTaskCommand(project.Id, "Write docs", null, TaskPriority.High));

        task.State.Should().Be("Todo");
        task.Priority.Should().Be("High");

        var reloaded = await Sender.Send(new GetProjectQuery(project.Id));
        reloaded.OpenTasks.Should().Be(1);
    }

    [Fact]
    public async Task A_domain_rule_surfaces_as_a_DomainException_not_a_crash()
    {
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));
        var task = await Sender.Send(new CreateTaskCommand(project.Id, "Unassigned", null));

        var act = async () => await Sender.Send(new MoveTaskCommand(task.Id, TaskState.Done));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Completing_stamps_the_configured_clock()
    {
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));
        var task = await Sender.Send(new CreateTaskCommand(project.Id, "Write docs", null));

        await Sender.Send(new AssignTaskCommand(task.Id, "bob"));
        await Sender.Send(new MoveTaskCommand(task.Id, TaskState.InReview));

        _clock.UtcNow = _clock.UtcNow.AddDays(3);
        var done = await Sender.Send(new MoveTaskCommand(task.Id, TaskState.Done));

        done.CompletedAt.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task Tasks_are_ordered_by_priority_rank_not_spelling()
    {
        // Stored as text, SQL would sort "High" below "Low".
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));

        foreach (var priority in new[] { TaskPriority.Low, TaskPriority.Critical, TaskPriority.Normal, TaskPriority.High })
        {
            await Sender.Send(new CreateTaskCommand(project.Id, $"{priority} task", null, priority));
        }

        var page = await Sender.Send(new GetTasksQuery(project.Id));

        page.Items.Select(t => t.Priority).Should().Equal("Critical", "High", "Normal", "Low");
    }

    [Fact]
    public async Task Filtering_by_minimum_priority_uses_rank()
    {
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));
        await Sender.Send(new CreateTaskCommand(project.Id, "low", null, TaskPriority.Low));
        await Sender.Send(new CreateTaskCommand(project.Id, "high", null, TaskPriority.High));
        await Sender.Send(new CreateTaskCommand(project.Id, "critical", null, TaskPriority.Critical));

        var page = await Sender.Send(new GetTasksQuery(project.Id, MinimumPriority: TaskPriority.High));

        page.Items.Select(t => t.Priority).Should().BeEquivalentTo(["Critical", "High"]);
    }

    [Fact]
    public async Task Paging_reports_totals_and_clamps_an_abusive_page_size()
    {
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));
        for (var i = 0; i < 25; i++)
        {
            await Sender.Send(new CreateTaskCommand(project.Id, $"Task {i}", null));
        }

        var page = await Sender.Send(new GetTasksQuery(project.Id, Page: 2, PageSize: 10));

        page.Items.Should().HaveCount(10);
        page.TotalCount.Should().Be(25);
        page.TotalPages.Should().Be(3);
        page.HasNextPage.Should().BeTrue();

        // A client asking for a million rows gets 100.
        var huge = await Sender.Send(new GetTasksQuery(project.Id, PageSize: 1_000_000));
        huge.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Overdue_filtering_uses_the_clock()
    {
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));
        await Sender.Send(new CreateTaskCommand(project.Id, "due soon", null, TaskPriority.Normal, _clock.UtcNow.AddDays(1)));
        await Sender.Send(new CreateTaskCommand(project.Id, "no due date", null));

        var beforeDue = await Sender.Send(new GetTasksQuery(project.Id, OverdueOnly: true));
        beforeDue.Items.Should().BeEmpty();

        _clock.UtcNow = _clock.UtcNow.AddDays(2);

        var afterDue = await Sender.Send(new GetTasksQuery(project.Id, OverdueOnly: true));
        afterDue.Items.Should().ContainSingle().Which.Title.Should().Be("due soon");
    }

    [Fact]
    public async Task Audit_fields_are_stamped_by_the_DbContext_not_the_handler()
    {
        _user.UserId = "alice";
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));

        var context = _provider.GetRequiredService<ApplicationDbContext>();
        var stored = await context.ProjectSet.AsNoTracking().FirstAsync(p => p.Id == project.Id);

        stored.CreatedBy.Should().Be("alice");
        stored.CreatedAt.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task Deleting_a_task_cancels_it_so_the_audit_trail_survives()
    {
        var project = await Sender.Send(new CreateProjectCommand("Apollo", null));
        var task = await Sender.Send(new CreateTaskCommand(project.Id, "Write docs", null));

        await Sender.Send(new DeleteTaskCommand(task.Id));

        var reloaded = await Sender.Send(new GetTaskQuery(task.Id));
        reloaded.State.Should().Be("Cancelled", "the row is still there");
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
