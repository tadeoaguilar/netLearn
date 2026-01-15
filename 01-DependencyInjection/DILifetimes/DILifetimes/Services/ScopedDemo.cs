namespace DILifetimes.Services;



public class ScopedDemo
{
    private readonly IScopedService _service1;
    private readonly IScopedService _service2;

    public ScopedDemo(IScopedService service1, IScopedService service2)
    {
        _service1 = service1;
        _service2 = service2;
    }

    public void ShowIds()
    {
        Console.WriteLine($"  ScopedDemo - Service 1 ID: {_service1.InstanceId}");
        Console.WriteLine($"  ScopedDemo - Service 2 ID: {_service2.InstanceId}");
        Console.WriteLine($"  Are they the same? {_service1.InstanceId == _service2.InstanceId}");
    }
}

public interface IScopedService
{
    Guid InstanceId { get; }
    void DoWork();
}

public class ScopedService : IScopedService
{
    private readonly Guid _instanceId;

    public ScopedService()
    {
        _instanceId = Guid.NewGuid();
        Console.WriteLine($"[SCOPED] Created instance {_instanceId}");
    }

    public Guid InstanceId => _instanceId;

    public void DoWork()
    {
        Console.WriteLine($"[SCOPED] Working with instance {_instanceId}");
    }
}