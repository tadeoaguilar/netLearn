namespace AdvancedDI.Services;

/// <summary>
/// Part 5: one abstraction, two implementations picked at startup by
/// environment. Consumers are identical in both cases.
/// </summary>
public interface ICacheService
{
    void Set(string key, string value);
    string? Get(string key);
}

public class RedisCacheService : ICacheService
{
    private readonly Dictionary<string, string> _cache = new();

    public RedisCacheService()
    {
        Console.WriteLine("[REDIS] Redis cache service initialized (production)");
    }

    public void Set(string key, string value)
    {
        _cache[key] = value;
        Console.WriteLine($"[REDIS] Set {key} = {value}");
    }

    public string? Get(string key)
    {
        _cache.TryGetValue(key, out var value);
        Console.WriteLine($"[REDIS] Get {key} = {value ?? "null"}");
        return value;
    }
}

public class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, string> _cache = new();

    public InMemoryCacheService()
    {
        Console.WriteLine("[MEMORY] In-memory cache service initialized (development)");
    }

    public void Set(string key, string value)
    {
        _cache[key] = value;
        Console.WriteLine($"[MEMORY] Set {key} = {value}");
    }

    public string? Get(string key)
    {
        _cache.TryGetValue(key, out var value);
        Console.WriteLine($"[MEMORY] Get {key} = {value ?? "null"}");
        return value;
    }
}
