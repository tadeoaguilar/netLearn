namespace AsyncAwait.Examples;

/// <summary>Part 5: what ConfigureAwait(false) actually controls.</summary>
public class ConfigureAwaitExample
{
    public async Task DefaultBehavior()
    {
        Console.WriteLine($"[Before await] Thread: {Environment.CurrentManagedThreadId}");

        await Task.Delay(100);

        // In a console app there is no SynchronizationContext to capture, so
        // this usually resumes on a pool thread anyway. In WinForms, WPF or
        // classic ASP.NET the difference is real -- and so are the deadlocks.
        Console.WriteLine($"[After await] Thread: {Environment.CurrentManagedThreadId}");
    }

    public async Task WithConfigureAwaitFalse()
    {
        Console.WriteLine($"[Before await] Thread: {Environment.CurrentManagedThreadId}");

        await Task.Delay(100).ConfigureAwait(false);

        Console.WriteLine($"[After await] Thread: {Environment.CurrentManagedThreadId}");
    }

    /// <summary>
    /// Library code should not care which thread it resumes on, so it opts out
    /// of context capture at every await.
    /// </summary>
    /// <remarks>
    /// EXERCISE.md declares this as `async Task` while returning a value, which
    /// does not compile. The return type has to be Task&lt;string&gt;.
    /// </remarks>
    public async Task<string> LibraryMethodExample()
    {
        var data = await FetchDataAsync().ConfigureAwait(false);
        var processed = await ProcessDataAsync(data).ConfigureAwait(false);
        return processed;
    }

    private async Task<string> FetchDataAsync()
    {
        await Task.Delay(100).ConfigureAwait(false);
        return "data";
    }

    private async Task<string> ProcessDataAsync(string data)
    {
        await Task.Delay(100).ConfigureAwait(false);
        return data.ToUpperInvariant();
    }
}
