using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.Enums;

namespace CleanArchitecture.Application.DTOs;

/// <summary>
/// DTOs exist so entities never cross the boundary. Returning a TaskItem from
/// an endpoint would expose its private setters to serialization, leak domain
/// changes into the API contract, and invite callers to depend on internals.
/// </summary>
public record TaskDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    string Priority,
    string State,
    string? Assignee,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static TaskDto From(TaskItem task) => new(
        task.Id,
        task.ProjectId,
        task.Title,
        task.Description,
        task.Priority.ToString(),
        task.State.ToString(),
        task.Assignee,
        task.CreatedAt,
        task.CompletedAt);
}

public record ProjectDto(
    Guid Id,
    string Name,
    bool IsArchived,
    int TotalTasks,
    int OpenTasks,
    DateTimeOffset CreatedAt)
{
    public static ProjectDto From(Project project) => new(
        project.Id,
        project.Name,
        project.IsArchived,
        project.Tasks.Count,
        project.Tasks.Count(t => t.State is TaskState.Todo or TaskState.InProgress),
        project.CreatedAt);
}
