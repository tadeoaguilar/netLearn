using System.Diagnostics;

namespace TaskParallelLibrary.Examples;

/// <summary>
/// Part 7: the decision that matters most in this whole module.
/// Async is for waiting. Parallel is for computing.
/// </summary>
public class AsyncVsParallel
{
    // CORRECT: async for I/O. No thread is held during the wait.
    public async Task IOBoundOperationAsync()
    {
        var tasks = new[]
        {
            DownloadAsync("https://api1.com"),
            DownloadAsync("https://api2.com"),
            DownloadAsync("https://api3.com")
        };

        await Task.WhenAll(tasks);
    }

    private static async Task<string> DownloadAsync(string url)
    {
        await Task.Delay(1000); // Simulating I/O
        return $"data from {url}";
    }

    // CORRECT: parallel for CPU work. Threads are the point.
    public void CpuBoundOperation()
    {
        var numbers = Enumerable.Range(1, 1_000_000).ToArray();
        var results = new double[numbers.Length];

        Parallel.For(0, numbers.Length, i =>
        {
            results[i] = Math.Sqrt(numbers[i]) * Math.Log(numbers[i]);
        });
    }

    // WRONG: parallel for I/O. Ten threads, all asleep.
    public void WrongParallelForIO()
    {
        Parallel.For(0, 10, _ => Thread.Sleep(1000));
    }

    // WRONG: a task per trivial calculation. Scheduling costs more than
    // the arithmetic does.
    public async Task WrongAsyncForCpu()
    {
        var tasks = Enumerable.Range(1, 100)
            .Select(i => Task.Run(() => Math.Sqrt(i)))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    public async Task DemonstrateTheDifference()
    {
        Console.WriteLine("=== I/O-BOUND: 10 waits of 500ms ===\n");

        var sw = Stopwatch.StartNew();
        Parallel.For(0, 10, _ => Thread.Sleep(500));
        Console.WriteLine($"Parallel.For:  {sw.ElapsedMilliseconds}ms, " +
                          $"{Environment.ProcessorCount} threads blocked doing nothing");

        sw.Restart();
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Delay(500)));
        Console.WriteLine($"Task.WhenAll:  {sw.ElapsedMilliseconds}ms, no thread held at all");

        Console.WriteLine("\nSimilar wall-clock time, completely different thread cost.");
        Console.WriteLine("At 10 items nobody notices. At 10,000 concurrent requests,");
        Console.WriteLine("the Parallel.For version exhausts the thread pool and the");
        Console.WriteLine("async version is still fine.");
    }
}
