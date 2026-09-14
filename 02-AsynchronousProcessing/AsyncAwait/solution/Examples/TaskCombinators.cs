namespace AsyncAwait.Examples;

/// <summary>Part 2: combining tasks with WhenAll and WhenAny.</summary>
public class TaskCombinators
{
    private async Task<int> ProcessDataAsync(int id, int delayMs)
    {
        Console.WriteLine($"[Task {id}] Starting...");
        await Task.Delay(delayMs);
        Console.WriteLine($"[Task {id}] Completed");
        return id * 10;
    }

    public async Task DemonstrateWhenAll()
    {
        Console.WriteLine("=== Task.WhenAll ===\n");

        var tasks = new[]
        {
            ProcessDataAsync(1, 1000),
            ProcessDataAsync(2, 2000),
            ProcessDataAsync(3, 1500)
        };

        // Results come back in the order the tasks were supplied, NOT the
        // order they finished. Task 2 finishes last but is still results[1].
        int[] results = await Task.WhenAll(tasks);

        Console.WriteLine($"\nAll tasks completed!");
        Console.WriteLine($"Results: {string.Join(", ", results)}");
    }

    public async Task DemonstrateWhenAny()
    {
        Console.WriteLine("\n=== Task.WhenAny ===\n");

        var tasks = new[]
        {
            ProcessDataAsync(1, 3000),
            ProcessDataAsync(2, 1000),  // This will finish first
            ProcessDataAsync(3, 2000)
        };

        Task<int> completedTask = await Task.WhenAny(tasks);
        int firstResult = await completedTask;

        Console.WriteLine($"\nFirst task completed with result: {firstResult}");
        Console.WriteLine("Other tasks still running...\n");

        // WhenAny does not cancel the losers -- they keep going. Await them so
        // their exceptions are observed rather than lost.
        await Task.WhenAll(tasks);
        Console.WriteLine("All tasks now complete");
    }
}
