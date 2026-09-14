using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Infrastructure.Persistence;

namespace TaskManagement.Tests;

/// <summary>
/// The schema is part of the contract: anyone writing a report, a migration or
/// a psql query depends on these names. Pinning them here means a rename is a
/// deliberate act rather than an accident.
/// </summary>
public class NamingTests
{
    [Theory]
    [InlineData("Id", "id")]
    [InlineData("Name", "name")]
    [InlineData("OwnerId", "owner_id")]
    [InlineData("ProjectId", "project_id")]
    [InlineData("DueDate", "due_date")]
    [InlineData("LastModifiedBy", "last_modified_by")]
    [InlineData("CompletedAt", "completed_at")]
    [InlineData("PK_projects", "pk_projects")]
    [InlineData("IX_tasks_ProjectId", "ix_tasks_project_id")]
    [InlineData("FK_tasks_projects_ProjectId", "fk_tasks_projects_project_id")]
    public void Names_are_converted_to_snake_case(string input, string expected)
    {
        SnakeCaseNaming.ToSnakeCase(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("TaskDTOId", "task_dto_id")]
    [InlineData("HTTPStatus", "http_status")]
    [InlineData("Address1", "address1")]
    public void Runs_of_capitals_are_not_split_letter_by_letter(string input, string expected)
    {
        SnakeCaseNaming.ToSnakeCase(input).Should().Be(expected);
    }

    [Fact]
    public void Every_mapped_column_is_snake_case()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options,
            new TestUser(),
            new FixedClock());

        var columns = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Select(p => p.GetColumnName())
            .ToArray();

        columns.Should().NotBeEmpty();
        columns.Should().OnlyContain(name => name == name!.ToLowerInvariant(),
            "a PascalCase column can only be referenced quoted in PostgreSQL");
        columns.Should().NotContain(name => name!.Contains("__"));
    }

    [Fact]
    public void The_expected_schema_is_produced()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options,
            new TestUser(),
            new FixedClock());

        context.Database.EnsureCreated();

        var projects = context.Model.FindEntityType(typeof(Domain.Entities.Project))!;
        projects.GetTableName().Should().Be("projects");
        projects.GetProperties().Select(p => p.GetColumnName())
            .Should().Contain(["id", "name", "description", "owner_id", "status", "created_at", "created_by"]);

        var tasks = context.Model.FindEntityType(typeof(Domain.Entities.TaskItem))!;
        tasks.GetTableName().Should().Be("tasks");
        tasks.GetProperties().Select(p => p.GetColumnName())
            .Should().Contain(["id", "project_id", "title", "priority", "state", "assignee_id", "due_date"]);
    }
}
