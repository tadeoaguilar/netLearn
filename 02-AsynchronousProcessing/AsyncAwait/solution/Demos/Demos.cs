using AsyncAwait.Challenge;
using AsyncAwait.Examples;

namespace AsyncAwait.Demos;

public static class Demos
{
    public static async Task Part1SyncVsAsync()
    {
        Console.WriteLine("=== PART 1: SYNC VS ASYNC ===\n");
        var demo = new SyncVsAsync();
        demo.DemonstrateDifference();
        await demo.DemonstrateDifferenceAsync();
        Console.WriteLine("Sequential blocking costs ~6000ms; overlapping costs ~2000ms.");
    }

    public static async Task Part2Combinators()
    {
        Console.WriteLine("=== PART 2: WHENALL / WHENANY ===\n");
        var demo = new TaskCombinators();
        await demo.DemonstrateWhenAll();
        await demo.DemonstrateWhenAny();
    }

    public static async Task Part3Cancellation()
    {
        Console.WriteLine("=== PART 3: CANCELLATION ===\n");
        var demo = new CancellationExample();
        await demo.DemonstrateCancellation();
        await demo.DemonstrateManualCancellation();
    }

    public static async Task Part4Exceptions()
    {
        Console.WriteLine("=== PART 4: EXCEPTION HANDLING ===\n");
        var demo = new ExceptionHandling();
        await demo.DemonstrateSingleException();
        await demo.DemonstrateMultipleExceptions();
        await demo.DemonstrateAllExceptions();
    }

    public static async Task Part5ConfigureAwait()
    {
        Console.WriteLine("=== PART 5: CONFIGUREAWAIT ===\n");
        var demo = new ConfigureAwaitExample();
        await demo.DefaultBehavior();
        await demo.WithConfigureAwaitFalse();
        Console.WriteLine($"\nLibrary method result: {await demo.LibraryMethodExample()}");
        Console.WriteLine("\nA console app has no SynchronizationContext, so the thread");
        Console.WriteLine("ids look similar either way. In WPF or WinForms they would not.");
    }

    public static async Task Part6Pitfalls()
    {
        Console.WriteLine("=== PART 6: PITFALLS ===\n");
        var demo = new AsyncPitfalls();

        Console.WriteLine("Correct: awaiting a collection of tasks");
        await demo.CollectAndAwaitCorrect();
        Console.WriteLine("  all five completed before this line ran\n");

        Console.WriteLine("Wrong: fire and forget");
        await demo.FireAndForgetWrong();
        Console.WriteLine("  this line ran while those five were still in flight\n");

        Console.WriteLine("Open solution/Examples/Pitfalls.cs -- the #pragma directives");
        Console.WriteLine("mark every place the compiler had to be silenced to write a bug.");
    }

    public static async Task Part7DataProcessor()
    {
        Console.WriteLine("=== PART 7: ASYNC DATA PROCESSOR ===\n");
        await new DataProcessor().DemonstrateWithTimeout();
    }

    public static async Task ChallengeDownloadManager()
    {
        Console.WriteLine("=== CHALLENGE: ASYNC DOWNLOAD MANAGER ===\n");

        var manager = new DownloadManager(
            new SimulatedFileDownloader(),
            new DownloadOptions
            {
                MaxAttempts = 3,
                Timeout = TimeSpan.FromSeconds(2),
                MaxConcurrency = 3
            });

        var urls = new[]
        {
            "https://files.example.com/report.pdf",
            "https://files.example.com/flaky-archive.zip",   // fails twice, then works
            "https://files.example.com/broken-image.png",    // always fails
            "https://files.example.com/slow-video.mp4",      // hangs until timeout
            "https://files.example.com/notes.txt"
        };

        var progress = new Progress<DownloadProgress>(p =>
        {
            if (p.PercentComplete == 100)
            {
                Console.WriteLine($"  [{p.PercentComplete,3}%] {Shorten(p.Url)} (attempt {p.Attempt})");
            }
        });

        var stats = await manager.DownloadAllAsync(urls, progress);

        Console.WriteLine($"\n--- Per file ---");
        foreach (var result in stats.Results)
        {
            var status = result.Succeeded
                ? $"ok      {result.Bytes,6:N0} bytes in {result.Attempts} attempt(s)"
                : $"FAILED  after {result.Attempts} attempt(s): {result.Error}";
            Console.WriteLine($"  {Shorten(result.Url),-20} {status}");
        }

        Console.WriteLine($"\n--- Statistics ---");
        Console.WriteLine($"  Total:      {stats.Total}");
        Console.WriteLine($"  Succeeded:  {stats.Succeeded}");
        Console.WriteLine($"  Failed:     {stats.Failed}");
        Console.WriteLine($"  Bytes:      {stats.TotalBytes:N0}");
        Console.WriteLine($"  Elapsed:    {stats.Elapsed.TotalSeconds:0.00}s");
        Console.WriteLine($"  Success:    {stats.SuccessRate:P0}");

        Console.WriteLine("\nThe flaky URL succeeded on attempt 3; the slow one burned");
        Console.WriteLine("three timeouts before giving up. Both are retry policy, not luck.");
    }

    private static string Shorten(string url) => url[(url.LastIndexOf('/') + 1)..];
}
