using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CleanArchitecture.Application.DTOs;
using CleanArchitecture.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Tests;

/// <summary>
/// The whole stack: real HTTP, real endpoints, real EF Core, real SQLite.
///
/// The database is an in-memory SQLite connection held open for the fixture's
/// lifetime, so these run in milliseconds and leave nothing behind -- but they
/// still exercise the actual provider, which is where mapping bugs live.
/// EF Core's InMemory provider would not have caught the DateTimeOffset
/// ORDER BY failure or the lexicographic priority sort.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace the file-backed context the app registers.
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public class IntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public IntegrationTests(ApiFactory factory) => _client = factory.CreateClient();

    private async Task<ProjectDto> CreateProjectAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/projects", new { name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectDto>(Json))!;
    }

    private async Task<TaskDto> CreateTaskAsync(Guid projectId, string title, string priority = "Normal")
    {
        var response = await _client.PostAsJsonAsync(
            $"/projects/{projectId}/tasks", new { title, priority });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<TaskDto>(Json))!;
    }

    [Fact]
    public async Task The_full_lifecycle_works_over_HTTP()
    {
        var project = await CreateProjectAsync($"Apollo-{Guid.NewGuid():N}");
        var task = await CreateTaskAsync(project.Id, "Write the docs", "High");

        task.State.Should().Be("Todo");

        var assign = await _client.PostAsJsonAsync($"/tasks/{task.Id}/assign", new { assignee = "tadeo" });
        assign.StatusCode.Should().Be(HttpStatusCode.OK);
        (await assign.Content.ReadFromJsonAsync<TaskDto>(Json))!.State.Should().Be("InProgress");

        var complete = await _client.PostAsync($"/tasks/{task.Id}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);

        var completed = (await complete.Content.ReadFromJsonAsync<TaskDto>(Json))!;
        completed.State.Should().Be("Done");
        completed.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_broken_business_rule_becomes_409_not_500()
    {
        var project = await CreateProjectAsync($"Rules-{Guid.NewGuid():N}");
        var task = await CreateTaskAsync(project.Id, "Unassigned");

        var response = await _client.PostAsync($"/tasks/{task.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invalid_input_becomes_400()
    {
        var project = await CreateProjectAsync($"Validation-{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync(
            $"/projects/{project.Id}/tasks", new { title = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_missing_entity_becomes_404()
    {
        var response = await _client.PostAsync($"/tasks/{Guid.NewGuid()}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Priority_ordering_survives_the_round_trip_through_SQLite()
    {
        // The regression test for a bug this suite actually caught: with the
        // enum stored as text, SQL ordered it lexicographically and "High"
        // came out below "Low".
        var project = await CreateProjectAsync($"Ordering-{Guid.NewGuid():N}");

        await CreateTaskAsync(project.Id, "the low one", "Low");
        await CreateTaskAsync(project.Id, "the urgent one", "Urgent");
        await CreateTaskAsync(project.Id, "the normal one", "Normal");
        await CreateTaskAsync(project.Id, "the high one", "High");

        var tasks = await _client.GetFromJsonAsync<List<TaskDto>>(
            $"/tasks?projectId={project.Id}", Json);

        tasks!.Select(t => t.Priority).Should().Equal("Urgent", "High", "Normal", "Low");
    }

    [Fact]
    public async Task Filtering_by_minimum_priority_uses_rank_not_spelling()
    {
        var project = await CreateProjectAsync($"MinPriority-{Guid.NewGuid():N}");
        await CreateTaskAsync(project.Id, "low", "Low");
        await CreateTaskAsync(project.Id, "high", "High");
        await CreateTaskAsync(project.Id, "urgent", "Urgent");

        var tasks = await _client.GetFromJsonAsync<List<TaskDto>>(
            $"/tasks?projectId={project.Id}&minimumPriority=High", Json);

        tasks!.Select(t => t.Priority).Should().BeEquivalentTo(["Urgent", "High"]);
    }

    [Fact]
    public async Task Timestamps_survive_the_DateTimeOffset_conversion()
    {
        // CreatedAt is stored as UTC ticks because SQLite cannot ORDER BY a
        // DateTimeOffset. Check the value comes back intact.
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var project = await CreateProjectAsync($"Time-{Guid.NewGuid():N}");
        var task = await CreateTaskAsync(project.Id, "timed");

        var fetched = await _client.GetFromJsonAsync<List<TaskDto>>(
            $"/tasks?projectId={project.Id}", Json);

        fetched!.Single().CreatedAt.Should().BeAfter(before)
            .And.BeCloseTo(task.CreatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Filtering_by_state_and_assignee_works_together()
    {
        var project = await CreateProjectAsync($"Filters-{Guid.NewGuid():N}");
        var mine = await CreateTaskAsync(project.Id, "mine");
        await CreateTaskAsync(project.Id, "someone elses");

        await _client.PostAsJsonAsync($"/tasks/{mine.Id}/assign", new { assignee = "tadeo" });
        await _client.PostAsync($"/tasks/{mine.Id}/complete", null);

        var done = await _client.GetFromJsonAsync<List<TaskDto>>(
            $"/tasks?projectId={project.Id}&state=Done&assignee=tadeo", Json);

        done!.Should().ContainSingle().Which.Title.Should().Be("mine");
    }

    [Fact]
    public async Task Archiving_reports_409_while_tasks_are_open_and_200_once_they_are_not()
    {
        var project = await CreateProjectAsync($"Archive-{Guid.NewGuid():N}");
        var task = await CreateTaskAsync(project.Id, "blocker");

        var blocked = await _client.PostAsync($"/projects/{project.Id}/archive", null);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await _client.PostAsJsonAsync($"/tasks/{task.Id}/assign", new { assignee = "tadeo" });
        await _client.PostAsync($"/tasks/{task.Id}/complete", null);

        var allowed = await _client.PostAsync($"/projects/{project.Id}/archive", null);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
