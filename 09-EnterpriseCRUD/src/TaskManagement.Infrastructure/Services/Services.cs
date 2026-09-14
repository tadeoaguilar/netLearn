using TaskManagement.Application.Common.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Used when nothing is signed in -- background jobs, migrations, and the
/// design-time factory. Returning a null user id rather than throwing keeps
/// those paths working without pretending someone is logged in.
/// </summary>
public class SystemUser : ICurrentUser
{
    public string? UserId => null;
    public string? UserName => null;
    public bool IsAuthenticated => false;
    public bool IsInRole(string role) => false;
}
