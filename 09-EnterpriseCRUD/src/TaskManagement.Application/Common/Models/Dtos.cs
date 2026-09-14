using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Common.Models;

public record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    string OwnerId,
    string Status,
    int TotalTasks,
    int OpenTasks,
    DateTimeOffset CreatedAt)
{
    public static ProjectDto From(Project project) => new(
        project.Id,
        project.Name,
        project.Description,
        project.OwnerId,
        project.Status.ToString(),
        project.Tasks.Count,
        project.OpenTaskCount,
        project.CreatedAt);
}

public record TaskDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    string Priority,
    string State,
    string? AssigneeId,
    DateTimeOffset? DueDate,
    DateTimeOffset? CompletedAt,
    bool IsOverdue,
    DateTimeOffset CreatedAt)
{
    public static TaskDto From(TaskItem task, DateTimeOffset now) => new(
        task.Id,
        task.ProjectId,
        task.Title,
        task.Description,
        task.Priority.ToString(),
        task.State.ToString(),
        task.AssigneeId,
        task.DueDate,
        task.CompletedAt,
        task.IsOverdue(now),
        task.CreatedAt);
}

/// <summary>
/// A page of results. Returning an unbounded list from a query endpoint is
/// fine until the table has a million rows, at which point it is an outage.
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
}
