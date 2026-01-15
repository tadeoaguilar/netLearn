namespace BasicDI.WithDI;

public interface IRequestIdGenerator
{
    Guid GetRequestId();
}

public class RequestIdGenerator : IRequestIdGenerator
{
    private readonly Guid _id;

    public RequestIdGenerator()
    {
        _id = Guid.NewGuid();
        Console.WriteLine($"[RequestIdGenerator] Created new instance with ID: {_id}");
    }

    public Guid GetRequestId() => _id;
}