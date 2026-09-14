using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Common.Database;

namespace VerticalSlice.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
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

/// <summary>
/// The same scenarios as module 03's IntegrationTests, against the same HTTP
/// contract. Both architectures must be indistinguishable from outside -- that
/// is what makes the comparison between them fair.
/// </summary>
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
        var response = await _client.PostAsJsonAsync($"/projects/{projectId}/tasks", new { title, priority });
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
        (await assign.Content.ReadFromJsonAsync<TaskDto>(Json))!.State.Should().Be("InProgress");

        var complete = await _client.PostAsync($"/tasks/{task.Id}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        (await complete.Content.ReadFromJsonAsync<TaskDto>(Json))!.State.Should().Be("Done");
    }

    [Fact]
    public async Task A_broken_rule_becomes_409()
    {
        var project = await CreateProjectAsync($"Rules-{Guid.NewGuid():N}");
        var task = await CreateTaskAsync(project.Id, "Unassigned");

        var response = await _client.PostAsync($"/tasks/{task.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Validation_failures_become_400_with_per_field_errors()
    {
        // The ValidationBehavior runs before the handler, so the handler never
        // sees an invalid command. Module 03 gets the same 400 from a domain
        // exception instead -- same contract, different mechanism.
        var response = await _client.PostAsJsonAsync("/projects", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetProperty("Name")[0].GetString()
            .Should().Be("Project name is required.");
    }

    [Fact]
    public async Task A_missing_entity_becomes_404()
    {
        var response = await _client.PostAsync($"/tasks/{Guid.NewGuid()}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Priority_ordering_survives_the_round_trip()
    {
        var project = await CreateProjectAsync($"Ordering-{Guid.NewGuid():N}");

        await CreateTaskAsync(project.Id, "the low one", "Low");
        await CreateTaskAsync(project.Id, "the urgent one", "Urgent");
        await CreateTaskAsync(project.Id, "the normal one", "Normal");
        await CreateTaskAsync(project.Id, "the high one", "High");

        var tasks = await _client.GetFromJsonAsync<List<TaskDto>>($"/tasks?projectId={project.Id}", Json);

        tasks!.Select(t => t.Priority).Should().Equal("Urgent", "High", "Normal", "Low");
    }

    [Fact]
    public async Task Archiving_reports_409_then_200()
    {
        var project = await CreateProjectAsync($"Archive-{Guid.NewGuid():N}");
        var task = await CreateTaskAsync(project.Id, "blocker");

        (await _client.PostAsync($"/projects/{project.Id}/archive", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        await _client.PostAsJsonAsync($"/tasks/{task.Id}/assign", new { assignee = "tadeo" });
        await _client.PostAsync($"/tasks/{task.Id}/complete", null);

        (await _client.PostAsync($"/projects/{project.Id}/archive", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Every_slice_registered_its_own_endpoint()
    {
        // Endpoints are discovered by reflection, so a slice that forgets to
        // implement IEndpoint simply never appears. This catches that.
        var root = await _client.GetFromJsonAsync<JsonElement>("/");
        var declared = root.GetProperty("endpoints").EnumerateArray().Count();

        declared.Should().Be(7);

        var project = await CreateProjectAsync($"Smoke-{Guid.NewGuid():N}");
        (await _client.GetAsync("/projects")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/tasks?projectId={project.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
