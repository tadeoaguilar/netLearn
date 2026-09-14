using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Common.Models;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Tests;

/// <summary>
/// The real host, the real pipeline, real JWT validation and real SQLite.
///
/// The connection is held open for the fixture's lifetime so the in-memory
/// database survives between requests. Using EF's InMemory provider instead
/// would skip the very mapping bugs these tests exist to catch.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        // Development so the dev token endpoint is available.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
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
    private readonly ApiFactory _factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public IntegrationTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClientAsync(string userId = "alice", string roles = "user")
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/dev/token?userId={userId}&roles={roles}", null);
        response.EnsureSuccessStatusCode();

        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<ProjectDto> CreateProjectAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/projects", new { name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectDto>(Json))!;
    }

    private static async Task<TaskDto> CreateTaskAsync(
        HttpClient client, Guid projectId, string title, string priority = "Normal")
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks", new { title, priority });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<TaskDto>(Json))!;
    }

    [Fact]
    public async Task An_unauthenticated_request_is_rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/projects");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_garbage_token_is_rejected()
    {
        // The signature is actually verified -- this is not decorative.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        var response = await client.GetAsync("/api/v1/projects");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_full_lifecycle_works_over_HTTP()
    {
        var client = await ClientAsync();
        var project = await CreateProjectAsync(client, $"Apollo-{Guid.NewGuid():N}");
        var task = await CreateTaskAsync(client, project.Id, "Write the docs", "High");

        task.State.Should().Be("Todo");

        var assign = await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/assign", new { assigneeId = "bob" });
        (await assign.Content.ReadFromJsonAsync<TaskDto>(Json))!.State.Should().Be("InProgress");

        await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/move", new { state = "InReview" });

        var done = await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/move", new { state = "Done" });
        done.StatusCode.Should().Be(HttpStatusCode.OK);

        var completed = (await done.Content.ReadFromJsonAsync<TaskDto>(Json))!;
        completed.State.Should().Be("Done");
        completed.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_broken_business_rule_becomes_409()
    {
        var client = await ClientAsync();
        var project = await CreateProjectAsync(client, $"Rules-{Guid.NewGuid():N}");
        var task = await CreateTaskAsync(client, project.Id, "Unassigned");

        var response = await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/move", new { state = "Done" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("title").GetString().Should().Be("Business rule violated");
    }

    [Fact]
    public async Task Invalid_input_becomes_400_with_field_errors()
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/projects", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").GetProperty("Name")[0].GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_missing_resource_becomes_404()
    {
        var client = await ClientAsync();

        var response = await client.GetAsync($"/api/v1/projects/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Another_users_project_is_forbidden_but_an_admin_may_edit_it()
    {
        var alice = await ClientAsync("alice");
        var project = await CreateProjectAsync(alice, $"Owned-{Guid.NewGuid():N}");

        var mallory = await ClientAsync("mallory");
        var refused = await mallory.PutAsJsonAsync($"/api/v1/projects/{project.Id}",
            new { name = "Hijacked", status = "Active" });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await ClientAsync("carol", "admin");
        var allowed = await admin.PutAsJsonAsync($"/api/v1/projects/{project.Id}",
            new { name = "Renamed by admin", status = "Active" });

        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Priority_ordering_survives_the_round_trip_through_SQLite()
    {
        var client = await ClientAsync();
        var project = await CreateProjectAsync(client, $"Ordering-{Guid.NewGuid():N}");

        foreach (var priority in new[] { "Low", "Critical", "Normal", "High" })
        {
            await CreateTaskAsync(client, project.Id, $"{priority} task", priority);
        }

        var page = await client.GetFromJsonAsync<PagedResult<TaskDto>>(
            $"/api/v1/tasks?projectId={project.Id}", Json);

        page!.Items.Select(t => t.Priority).Should().Equal("Critical", "High", "Normal", "Low");
    }

    [Fact]
    public async Task Paging_metadata_is_correct()
    {
        var client = await ClientAsync();
        var project = await CreateProjectAsync(client, $"Paging-{Guid.NewGuid():N}");

        for (var i = 0; i < 12; i++) await CreateTaskAsync(client, project.Id, $"Task {i}");

        var page = await client.GetFromJsonAsync<PagedResult<TaskDto>>(
            $"/api/v1/tasks?projectId={project.Id}&page=2&pageSize=5", Json);

        page!.Items.Should().HaveCount(5);
        page.TotalCount.Should().Be(12);
        page.TotalPages.Should().Be(3);
        page.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Archiving_reports_409_while_tasks_are_open_and_200_once_they_are_not()
    {
        var client = await ClientAsync();
        var project = await CreateProjectAsync(client, $"Archive-{Guid.NewGuid():N}");
        var task = await CreateTaskAsync(client, project.Id, "blocker");

        (await client.PostAsync($"/api/v1/projects/{project.Id}/archive", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/assign", new { assigneeId = "bob" });
        await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/move", new { state = "InReview" });
        await client.PostAsJsonAsync($"/api/v1/tasks/{task.Id}/move", new { state = "Done" });

        (await client.PostAsync($"/api/v1/projects/{project.Id}/archive", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_endpoints_respond()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Audit_fields_record_the_authenticated_caller()
    {
        var client = await ClientAsync("dave");
        var project = await CreateProjectAsync(client, $"Audit-{Guid.NewGuid():N}");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await context.ProjectSet.AsNoTracking().FirstAsync(p => p.Id == project.Id);

        stored.CreatedBy.Should().Be("dave");
        stored.OwnerId.Should().Be("dave");
    }
}
