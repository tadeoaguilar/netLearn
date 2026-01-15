namespace DILifetimes.Services;

public interface ITransientService
{
    Guid InstanceId { get; }
    void DoWork();
}

public class TransientService : ITransientService
{
    private readonly Guid _instanceId;

    public TransientService()
    {
        _instanceId = Guid.NewGuid();
        Console.WriteLine($"[TRANSIENT] Created instance {_instanceId}");
    }

    public Guid InstanceId => _instanceId;

    public void DoWork()
    {
        Console.WriteLine($"[TRANSIENT] Working with instance {_instanceId}");
    }
}