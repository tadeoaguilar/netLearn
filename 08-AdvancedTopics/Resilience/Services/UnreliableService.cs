namespace Resilience.Services;

/// <summary>
/// A dependency that fails in the ways real ones do: transiently, persistently,
/// and by being slow rather than broken.
/// </summary>
public class UnreliableService
{
    private readonly Func<int, bool> _shouldFail;
    private readonly TimeSpan _latency;

    public UnreliableService(Func<int, bool> shouldFail, TimeSpan? latency = null)
    {
        _shouldFail = shouldFail;
        _latency = latency ?? TimeSpan.Zero;
    }

    public int Calls { get; private set; }

    public async Task<string> CallAsync(CancellationToken cancellationToken = default)
    {
        Calls++;

        if (_latency > TimeSpan.Zero)
        {
            await Task.Delay(_latency, cancellationToken);
        }

        if (_shouldFail(Calls))
        {
            throw new HttpRequestException($"upstream failed on call {Calls}");
        }

        return $"ok (call {Calls})";
    }

    /// <summary>Fails the first n calls, then recovers -- a transient fault.</summary>
    public static UnreliableService FailsFirst(int n) => new(call => call <= n);

    /// <summary>Always fails -- the case retrying cannot fix.</summary>
    public static UnreliableService AlwaysFails() => new(_ => true);

    /// <summary>Succeeds, but too slowly to be useful.</summary>
    public static UnreliableService Slow(TimeSpan latency) => new(_ => false, latency);
}
