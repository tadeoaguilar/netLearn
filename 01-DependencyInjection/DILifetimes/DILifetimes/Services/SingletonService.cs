namespace DILifetimes.Services;

public interface ISingletonService
{
    Guid InstanceId { get; }
    void DoWork();
}

public class SingletonService : ISingletonService
{
    private readonly Guid _instanceId;

    public SingletonService()
    {
        _instanceId = Guid.NewGuid();
        Console.WriteLine($"[SINGLETON] Created instance {_instanceId}");
    }

    public Guid InstanceId => _instanceId;

    public void DoWork()
    {
        Console.WriteLine($"[SINGLETON] Working with instance {_instanceId}");
    }
}