using System.Diagnostics;

namespace AsyncAwait.Examples;

/// <summary>
/// Part 1: the difference between blocking a thread and releasing it.
/// </summary>
public class SyncVsAsync
{
    // Synchronous - blocks the thread for the whole wait.
    public string DownloadDataSync(string url)
    {
        Console.WriteLine($"[SYNC] Starting download from {url}");
        Thread.Sleep(2000); // Simulating network delay
        Console.WriteLine($"[SYNC] Download complete from {url}");
        return $"Data from {url}";
    }

    // Asynchronous - the thread is free while the "network" is working.
    public async Task<string> DownloadDataAsync(string url)
    {
        Console.WriteLine($"[ASYNC] Starting download from {url}");
        await Task.Delay(2000); // Simulating network delay
        Console.WriteLine($"[ASYNC] Download complete from {url}");
        return $"Data from {url}";
    }

    public void DemonstrateDifference()
    {
        Console.WriteLine("=== SYNCHRONOUS ===");
        var sw = Stopwatch.StartNew();

        DownloadDataSync("https://api1.com");
        DownloadDataSync("https://api2.com");
        DownloadDataSync("https://api3.com");

        sw.Stop();
        Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms\n");
    }

    public async Task DemonstrateDifferenceAsync()
    {
        Console.WriteLine("=== ASYNCHRONOUS ===");
        var sw = Stopwatch.StartNew();

        // Starting the tasks without awaiting them is what creates the overlap.
        // Await each one in turn instead and you are back to 6000ms.
        var task1 = DownloadDataAsync("https://api1.com");
        var task2 = DownloadDataAsync("https://api2.com");
        var task3 = DownloadDataAsync("https://api3.com");

        await Task.WhenAll(task1, task2, task3);

        sw.Stop();
        Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms\n");
    }
}
