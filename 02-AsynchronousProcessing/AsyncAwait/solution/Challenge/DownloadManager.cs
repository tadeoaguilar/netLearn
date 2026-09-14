namespace AsyncAwait.Challenge;

/// <summary>
/// The thing that actually fetches bytes. The challenge says "use HttpClient",
/// but putting an interface here is what lets the tests run offline and in
/// milliseconds -- and it is the same dependency-inversion move from module 01.
/// </summary>
public interface IFileDownloader
{
    Task<byte[]> DownloadAsync(
        string url,
        IProgress<int>? progress,
        CancellationToken cancellationToken);
}

public record DownloadOptions
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public int MaxConcurrency { get; init; } = 4;
}

public record DownloadResult(
    string Url,
    bool Succeeded,
    int Attempts,
    long Bytes,
    string? Error);

public record DownloadStatistics(
    int Total,
    int Succeeded,
    int Failed,
    long TotalBytes,
    TimeSpan Elapsed,
    IReadOnlyList<DownloadResult> Results)
{
    public double SuccessRate => Total == 0 ? 0 : (double)Succeeded / Total;
}

/// <summary>
/// Downloads many files concurrently with per-download retry, per-download
/// timeout, progress reporting and overall statistics.
/// </summary>
public class DownloadManager
{
    private readonly IFileDownloader _downloader;
    private readonly DownloadOptions _options;

    public DownloadManager(IFileDownloader downloader, DownloadOptions? options = null)
    {
        _downloader = downloader;
        _options = options ?? new DownloadOptions();
    }

    public async Task<DownloadStatistics> DownloadAllAsync(
        IReadOnlyList<string> urls,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var start = TimeProvider.System.GetTimestamp();

        // A semaphore caps how many run at once. Without it, a thousand URLs
        // means a thousand simultaneous connections.
        using var gate = new SemaphoreSlim(_options.MaxConcurrency);

        var tasks = urls.Select(url =>
            DownloadOneGuardedAsync(url, gate, progress, cancellationToken));

        var results = await Task.WhenAll(tasks);

        return new DownloadStatistics(
            Total: results.Length,
            Succeeded: results.Count(r => r.Succeeded),
            Failed: results.Count(r => !r.Succeeded),
            TotalBytes: results.Sum(r => r.Bytes),
            Elapsed: TimeProvider.System.GetElapsedTime(start),
            Results: results);
    }

    private async Task<DownloadResult> DownloadOneGuardedAsync(
        string url,
        SemaphoreSlim gate,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await DownloadWithRetryAsync(url, progress, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<DownloadResult> DownloadWithRetryAsync(
        string url,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        string? lastError = null;

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            // A linked source means EITHER the caller cancelling OR this
            // download running long will cancel the attempt -- and afterwards
            // we can still tell the two apart.
            //
            // Note the limit of this approach: the timeout only works because
            // the downloader honours the token it is given. A download that
            // ignores its CancellationToken cannot be timed out this way --
            // this method awaits it directly, so it will simply hang. If you
            // must defend against that, race the call against a delay with
            // Task.WhenAny; but the abandoned work keeps running either way.
            using var timeoutCts = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.Timeout);

            try
            {
                var bytes = await _downloader.DownloadAsync(
                    url,
                    new Progress<int>(percent =>
                        progress?.Report(new DownloadProgress(url, percent, attempt))),
                    timeoutCts.Token);

                return new DownloadResult(url, true, attempt, bytes.LongLength, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller cancelled. Stop entirely rather than retrying.
                throw;
            }
            catch (OperationCanceledException)
            {
                // Only the timeout fired -- that is a retryable failure.
                lastError = $"Timed out after {_options.Timeout.TotalSeconds:0.##}s";
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new DownloadResult(url, false, _options.MaxAttempts, 0, lastError);
    }
}

public record DownloadProgress(string Url, int PercentComplete, int Attempt);
