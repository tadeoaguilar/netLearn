namespace AdvancedDI.Services;

/// <summary>
/// Part 6: a disposable, scope-bound resource. Watch the console output to see
/// that disposal happens when the scope ends, not when the last consumer does.
/// </summary>
public interface IDatabaseConnection
{
    Guid ConnectionId { get; }
    void ExecuteQuery(string query);
}

public class DatabaseConnection : IDatabaseConnection, IDisposable
{
    public Guid ConnectionId { get; }

    public DatabaseConnection()
    {
        ConnectionId = Guid.NewGuid();
        Console.WriteLine($"[DB] Connection opened: {ConnectionId}");
    }

    public void ExecuteQuery(string query)
    {
        Console.WriteLine($"[DB] Executing on {ConnectionId}: {query}");
    }

    public void Dispose()
    {
        Console.WriteLine($"[DB] Connection closed: {ConnectionId}");
        GC.SuppressFinalize(this);
    }
}

public class UnitOfWork
{
    private readonly IDatabaseConnection _connection;

    public UnitOfWork(IDatabaseConnection connection)
    {
        _connection = connection;
    }

    /// <summary>The connection this unit of work was given -- exposed so the
    /// demo and the tests can show two units of work sharing one scope.</summary>
    public Guid ConnectionId => _connection.ConnectionId;

    public void DoWork(string operation)
    {
        Console.WriteLine($"\n[UnitOfWork] Starting: {operation}");
        _connection.ExecuteQuery("BEGIN TRANSACTION");
        _connection.ExecuteQuery($"-- {operation} --");
        _connection.ExecuteQuery("COMMIT");
        Console.WriteLine($"[UnitOfWork] Completed: {operation}");
    }
}
