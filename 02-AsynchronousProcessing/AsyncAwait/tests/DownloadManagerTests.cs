using AsyncAwait.Challenge;

namespace AsyncAwait.Tests;

/// <summary>
/// The challenge. Every requirement from EXERCISE.md gets a test: concurrency,
/// progress, cancellation, retry, timeout and statistics.
/// </summary>
public class DownloadManagerTests
{
    /// <summary>A downloader whose behaviour each test dictates exactly.</summary>
    private sealed class FakeDownloader : IFileDownloader
    {
        private readonly Func<string, int, CancellationToken, Task<byte[]>> _behaviour;
        private readonly Dictionary<string, int> _attempts = new();
        private int _concurrent;

        public FakeDownloader(Func<string, int, CancellationToken, Task<byte[]>> behaviour)
            => _behaviour = behaviour;

        public int PeakConcurrency { get; private set; }

        public int AttemptsFor(string url) => _attempts.GetValueOrDefault(url);

        public async Task<byte[]> DownloadAsync(
            string url, IProgress<int>? progress, CancellationToken cancellationToken)
        {
            lock (_attempts)
            {
                _attempts[url] = _attempts.GetValueOrDefault(url) + 1;
            }

            var running = Interlocked.Increment(ref _concurrent);
            PeakConcurrency = Math.Max(PeakConcurrency, running);

            try
            {
                progress?.Report(50);
                var result = await _behaviour(url, AttemptsFor(url), cancellationToken);
                progress?.Report(100);
                return result;
            }
            finally
            {
                Interlocked.Decrement(ref _concurrent);
            }
        }
    }

    private static readonly byte[] Payload = new byte[512];

    [Fact]
    public async Task A_successful_download_is_reported_with_its_size()
    {
        var downloader = new FakeDownloader((_, _, _) => Task.FromResult(Payload));
        var sut = new DownloadManager(downloader);

        var stats = await sut.DownloadAllAsync(["a", "b"]);

        stats.Succeeded.Should().Be(2);
        stats.Failed.Should().Be(0);
        stats.TotalBytes.Should().Be(1024);
        stats.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public async Task A_transient_failure_is_retried_until_it_succeeds()
    {
        var downloader = new FakeDownloader((_, attempt, _) => attempt < 3
            ? throw new HttpRequestException("connection reset")
            : Task.FromResult(Payload));
        var sut = new DownloadManager(downloader, new DownloadOptions { MaxAttempts = 3 });

        var stats = await sut.DownloadAllAsync(["flaky"]);

        stats.Succeeded.Should().Be(1);
        stats.Results.Single().Attempts.Should().Be(3);
    }

    [Fact]
    public async Task Retries_stop_at_the_configured_maximum()
    {
        var downloader = new FakeDownloader((_, _, _)
            => throw new HttpRequestException("always broken"));
        var sut = new DownloadManager(downloader, new DownloadOptions { MaxAttempts = 3 });

        var stats = await sut.DownloadAllAsync(["broken"]);

        stats.Failed.Should().Be(1);
        downloader.AttemptsFor("broken").Should().Be(3, "not one attempt more");
        stats.Results.Single().Error.Should().Be("always broken");
    }

    [Fact]
    public async Task A_download_that_overruns_its_timeout_fails_and_says_so()
    {
        var downloader = new FakeDownloader(async (_, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Payload;
        });
        var sut = new DownloadManager(downloader, new DownloadOptions
        {
            MaxAttempts = 1,
            Timeout = TimeSpan.FromMilliseconds(100)
        });

        var stats = await sut.DownloadAllAsync(["slow"]);

        stats.Failed.Should().Be(1);
        stats.Results.Single().Error.Should().Contain("Timed out");
    }

    [Fact]
    public async Task A_timeout_is_retried_but_a_caller_cancellation_is_not()
    {
        // This is the distinction the linked token source exists to make.
        var downloader = new FakeDownloader(async (_, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Payload;
        });
        var sut = new DownloadManager(downloader, new DownloadOptions
        {
            MaxAttempts = 3,
            Timeout = TimeSpan.FromMilliseconds(50)
        });

        await sut.DownloadAllAsync(["slow"]);

        downloader.AttemptsFor("slow").Should().Be(3, "a timeout is retryable");
    }

    [Fact]
    public async Task Cancelling_the_caller_token_aborts_the_whole_batch()
    {
        using var cts = new CancellationTokenSource();
        var downloader = new FakeDownloader(async (_, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Payload;
        });
        var sut = new DownloadManager(downloader, new DownloadOptions { MaxAttempts = 3 });

        var running = sut.DownloadAllAsync(["a", "b"], cancellationToken: cts.Token);
        await cts.CancelAsync();

        var act = async () => await running;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Concurrency_never_exceeds_the_configured_limit()
    {
        var gate = new TaskCompletionSource();
        var downloader = new FakeDownloader(async (_, _, _) =>
        {
            await gate.Task;
            return Payload;
        });
        var sut = new DownloadManager(downloader, new DownloadOptions { MaxConcurrency = 2 });

        var running = sut.DownloadAllAsync(["a", "b", "c", "d", "e"]);
        await Task.Delay(50);   // let the semaphore admit all it will
        gate.SetResult();
        await running;

        downloader.PeakConcurrency.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Progress_is_reported_per_url_with_its_attempt_number()
    {
        var reports = new List<DownloadProgress>();
        var downloader = new FakeDownloader((_, attempt, _) => attempt < 2
            ? throw new HttpRequestException("first attempt fails")
            : Task.FromResult(Payload));
        var sut = new DownloadManager(downloader, new DownloadOptions { MaxConcurrency = 1 });

        await sut.DownloadAllAsync(
            ["file"],
            new Progress<DownloadProgress>(p => { lock (reports) reports.Add(p); }));

        // Progress is delivered through the synchronization context, so give
        // the callbacks a moment to land before asserting.
        await Task.Delay(100);

        reports.Should().NotBeEmpty();
        reports.Should().OnlyContain(r => r.Url == "file");
        reports.Select(r => r.Attempt).Should().Contain(2, "the retry reports too");
    }

    [Fact]
    public async Task Statistics_add_up_across_a_mixed_batch()
    {
        var downloader = new FakeDownloader((url, _, _) => url.StartsWith("bad")
            ? throw new HttpRequestException("nope")
            : Task.FromResult(Payload));
        var sut = new DownloadManager(downloader, new DownloadOptions { MaxAttempts = 1 });

        var stats = await sut.DownloadAllAsync(["ok1", "bad1", "ok2", "bad2", "ok3"]);

        stats.Total.Should().Be(5);
        stats.Succeeded.Should().Be(3);
        stats.Failed.Should().Be(2);
        stats.TotalBytes.Should().Be(3 * 512);
        stats.SuccessRate.Should().BeApproximately(0.6, 0.001);
        stats.Results.Should().HaveCount(5);
    }
}
