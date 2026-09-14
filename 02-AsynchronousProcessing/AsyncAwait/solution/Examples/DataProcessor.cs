namespace AsyncAwait.Examples;

/// <summary>Part 7: concurrent processing with cancellation and a timeout.</summary>
public class DataProcessor
{
    public async Task<List<string>> ProcessUrlsAsync(
        List<string> urls,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Processing {urls.Count} URLs...\n");

        // Select is lazy: the tasks only start when Task.WhenAll enumerates it.
        var tasks = urls.Select(url => FetchAndProcessAsync(url, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results.ToList();
    }

    private async Task<string> FetchAndProcessAsync(
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"[Fetching] {url}");
            await Task.Delay(Random.Shared.Next(500, 2000), cancellationToken);

            Console.WriteLine($"[Processing] {url}");
            await Task.Delay(500, cancellationToken);

            Console.WriteLine($"[Complete] {url}");
            return $"Processed: {url}";
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a failure -- rethrow so the caller can tell
            // "we stopped" apart from "this one broke".
            Console.WriteLine($"[Cancelled] {url}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] {url}: {ex.Message}");
            return $"Failed: {url}";
        }
    }

    public async Task DemonstrateWithTimeout()
    {
        var urls = new List<string>
        {
            "https://api.example.com/data1",
            "https://api.example.com/data2",
            "https://api.example.com/data3"
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            var results = await ProcessUrlsAsync(urls, cts.Token);
            Console.WriteLine($"\nProcessed {results.Count} URLs successfully");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nProcessing timed out!");
        }
    }
}
