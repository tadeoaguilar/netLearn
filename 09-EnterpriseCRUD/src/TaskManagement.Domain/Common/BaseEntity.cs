using TaskManagement.Domain.Events;

namespace TaskManagement.Domain.Common;

public abstract class BaseEntity
{
    private readonly List<DomainEvent> _domainEvents = new();

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents;

    protected void Raise(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>
/// Audit fields every persisted entity carries. Populated by the DbContext's
/// SaveChanges interceptor, never by hand -- relying on each handler to set
/// them is how half your rows end up with no CreatedBy.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}
