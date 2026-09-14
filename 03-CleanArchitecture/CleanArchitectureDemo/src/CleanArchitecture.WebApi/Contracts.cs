using CleanArchitecture.Domain.Enums;

namespace CleanArchitecture.WebApi;

/// <summary>
/// The API's own request shapes. They are deliberately separate from the
/// Application DTOs: a change to the HTTP contract should not force a change
/// to a use case, and vice versa.
/// </summary>
public record CreateProjectRequest(string Name);

public record CreateTaskRequest(string Title, string? Description, Priority Priority = Priority.Normal);

public record AssignTaskRequest(string Assignee);
