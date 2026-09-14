namespace TaskManagement.Domain.Exceptions;

/// <summary>A broken business rule. The API maps this to 409, never 500.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

/// <summary>An entity that does not exist. Mapped to 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entity, object key)
        : base($"{entity} with key '{key}' was not found.")
    {
        Entity = entity;
        Key = key;
    }

    public string Entity { get; }
    public object Key { get; }
}
