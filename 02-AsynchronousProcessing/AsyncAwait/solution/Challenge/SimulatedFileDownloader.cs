namespace AsyncAwait.Challenge;

/// <summary>
/// Stands in for a real HTTP download so the demo runs offline and
/// deterministically. Swap in an HttpClient-backed implementation and nothing
/// in <see cref="DownloadManager"/> changes -- that is the point of the
/// interface.
/// </summary>
public class SimulatedFileDownloader : IFileDownloader
{
    private readonly Dictionary<string, int> _attemptsByUrl = new();

    public async Task<byte[]> DownloadAsync(
        string url,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var attempt = _attemptsByUrl.TryGetValue(url, out var previous) ? previous + 1 : 1;
        _attemptsByUrl[url] = attempt;

        // "flaky" fails twice before succeeding; "broken" always fails;
        // "slow" hangs until the timeout cancels it.
        if (url.Contains("broken"))
        {
            await Task.Delay(50, cancellationToken);
            throw new HttpRequestException($"500 Internal Server Error for {url}");
        }

        if (url.Contains("flaky") && attempt <= 2)
        {
            await Task.Delay(50, cancellationToken);
            throw new HttpRequestException($"Connection reset for {url} (attempt {attempt})");
        }

        if (url.Contains("slow"))
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        for (var percent = 25; percent <= 100; percent += 25)
        {
            await Task.Delay(100, cancellationToken);
            progress?.Report(percent);
        }

        return new byte[1024 * attempt];
    }
}

/// <summary>A real implementation, for reference. Not used by the demo.</summary>
public class HttpFileDownloader : IFileDownloader
{
    private readonly HttpClient _httpClient;

    public HttpFileDownloader(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<byte[]> DownloadAsync(
        string url,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();

        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);

            if (total is > 0)
            {
                progress?.Report((int)(buffer.Length * 100 / total.Value));
            }
        }

        return buffer.ToArray();
    }
}
