# Exercise: Mastering Async/Await

## Overview
Learn to write efficient asynchronous code using async/await patterns. This is fundamental for building responsive, scalable applications in .NET.

## Learning Goals
- Understand async/await mechanics
- Learn when to use async vs sync
- Handle cancellation with CancellationToken
- Avoid common async pitfalls
- Use Task.WhenAll and Task.WhenAny
- Understand ConfigureAwait

---

## Part 1: Basic Async/Await

### Step 1.1: Synchronous vs Asynchronous

**Your Task:** Create `Examples/SyncVsAsync.cs`:

```csharp
namespace AsyncAwait.Examples;

public class SyncVsAsync
{
    // Synchronous - blocks the thread
    public string DownloadDataSync(string url)
    {
        Console.WriteLine($"[SYNC] Starting download from {url}");
        Thread.Sleep(2000); // Simulating network delay
        Console.WriteLine($"[SYNC] Download complete from {url}");
        return $"Data from {url}";
    }

    // Asynchronous - doesn't block
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
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var data1 = DownloadDataSync("https://api1.com");
        var data2 = DownloadDataSync("https://api2.com");
        var data3 = DownloadDataSync("https://api3.com");

        sw.Stop();
        Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms\n");
    }

    public async Task DemonstrateDifferenceAsync()
    {
        Console.WriteLine("=== ASYNCHRONOUS ===");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Run all downloads concurrently
        var task1 = DownloadDataAsync("https://api1.com");
        var task2 = DownloadDataAsync("https://api2.com");
        var task3 = DownloadDataAsync("https://api3.com");

        await Task.WhenAll(task1, task2, task3);

        sw.Stop();
        Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms\n");
    }
}
```

**Run it in Program.cs:**
```csharp
var demo = new SyncVsAsync();
demo.DemonstrateDifference();
await demo.DemonstrateDifferenceAsync();
```

**Observe:**
- Sync: 6000ms (2000ms × 3, sequential)
- Async: ~2000ms (concurrent execution)

---

## Part 2: Task.WhenAll and Task.WhenAny

### Step 2.1: Wait for All Tasks

**Your Task:** Create `Examples/TaskCombinators.cs`:

```csharp
namespace AsyncAwait.Examples;

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

        // Wait for ALL tasks to complete
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

        // Wait for FIRST task to complete
        Task<int> completedTask = await Task.WhenAny(tasks);
        int firstResult = await completedTask;

        Console.WriteLine($"\nFirst task completed with result: {firstResult}");
        Console.WriteLine("Other tasks still running...\n");

        // Wait for remaining tasks
        await Task.WhenAll(tasks);
        Console.WriteLine("All tasks now complete");
    }
}
```

**When to use:**
- `Task.WhenAll`: Need all results (parallel API calls)
- `Task.WhenAny`: First one wins (timeout, redundant requests)

---

## Part 3: Cancellation Tokens

### Step 3.1: Graceful Cancellation

**Your Task:** Create `Examples/CancellationExample.cs`:

```csharp
namespace AsyncAwait.Examples;

public class CancellationExample
{
    public async Task LongRunningOperationAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[Operation] Starting long operation...");

        for (int i = 1; i <= 10; i++)
        {
            // Check if cancellation requested
            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine($"[Operation] Processing step {i}/10");
            await Task.Delay(500, cancellationToken);
        }

        Console.WriteLine("[Operation] Operation completed successfully!");
    }

    public async Task DemonstrateCancellation()
    {
        Console.WriteLine("=== CANCELLATION EXAMPLE ===\n");

        using var cts = new CancellationTokenSource();

        // Cancel after 3 seconds
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            await LongRunningOperationAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n[Operation] Operation was cancelled!");
        }
    }

    public async Task DemonstrateManualCancellation()
    {
        Console.WriteLine("\n=== MANUAL CANCELLATION ===\n");

        using var cts = new CancellationTokenSource();

        var operation = LongRunningOperationAsync(cts.Token);

        // Simulate user cancelling after 2 seconds
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            Console.WriteLine("\n[User] Requesting cancellation...\n");
            cts.Cancel();
        });

        try
        {
            await operation;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Operation] Cancelled by user request!");
        }
    }
}
```

**Best Practice:** Always accept `CancellationToken` in async methods

---

## Part 4: Exception Handling

### Step 4.1: Handling Async Exceptions

**Your Task:** Create `Examples/ExceptionHandling.cs`:

```csharp
namespace AsyncAwait.Examples;

public class ExceptionHandling
{
    private async Task<string> FailingOperationAsync(int id)
    {
        await Task.Delay(1000);

        if (id == 2)
        {
            throw new InvalidOperationException($"Operation {id} failed!");
        }

        return $"Result {id}";
    }

    public async Task DemonstrateSingleException()
    {
        Console.WriteLine("=== SINGLE EXCEPTION ===\n");

        try
        {
            var result = await FailingOperationAsync(2);
            Console.WriteLine(result);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Caught exception: {ex.Message}");
        }
    }

    public async Task DemonstrateMultipleExceptions()
    {
        Console.WriteLine("\n=== MULTIPLE EXCEPTIONS (WhenAll) ===\n");

        var tasks = new[]
        {
            FailingOperationAsync(1),
            FailingOperationAsync(2), // Will throw
            FailingOperationAsync(3)
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            // Only first exception is thrown
            Console.WriteLine($"Caught: {ex.Message}\n");

            // To see all exceptions, check task exceptions
            foreach (var task in tasks.Where(t => t.IsFaulted))
            {
                Console.WriteLine($"Task exception: {task.Exception?.InnerException?.Message}");
            }
        }
    }
}
```

---

## Part 5: ConfigureAwait

### Step 5.1: Understanding ConfigureAwait

**Your Task:** Create `Examples/ConfigureAwaitExample.cs`:

```csharp
namespace AsyncAwait.Examples;

public class ConfigureAwaitExample
{
    public async Task DefaultBehavior()
    {
        Console.WriteLine($"[Before await] Thread: {Thread.CurrentThread.ManagedThreadId}");

        await Task.Delay(100);

        // By default, resumes on captured context
        Console.WriteLine($"[After await] Thread: {Thread.CurrentThread.ManagedThreadId}");
    }

    public async Task WithConfigureAwaitFalse()
    {
        Console.WriteLine($"[Before await] Thread: {Thread.CurrentThread.ManagedThreadId}");

        await Task.Delay(100).ConfigureAwait(false);

        // May resume on different thread
        Console.WriteLine($"[After await] Thread: {Thread.CurrentThread.ManagedThreadId}");
    }

    public async Task LibraryMethodExample()
    {
        // In library code, use ConfigureAwait(false)
        // to avoid capturing synchronization context
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
        return data.ToUpper();
    }
}
```

**Rule of thumb:**
- Application code: Usually don't need ConfigureAwait
- Library code: Use ConfigureAwait(false) to avoid context capture

---

## Part 6: Async Best Practices

### Step 6.1: Common Pitfalls to Avoid

**Your Task:** Create `Examples/Pitfalls.cs`:

```csharp
namespace AsyncAwait.Examples;

public class AsyncPitfalls
{
    // ❌ WRONG: Async void (except for event handlers)
    public async void DontDoThisAsync()
    {
        await Task.Delay(100);
        throw new Exception("Can't catch this!");
    }

    // ✅ CORRECT: Async Task
    public async Task DoThisAsync()
    {
        await Task.Delay(100);
    }

    // ❌ WRONG: Blocking on async code
    public void SyncOverAsyncWrong()
    {
        // Can cause deadlocks!
        var result = DoThisAsync().Result;
    }

    // ✅ CORRECT: Async all the way
    public async Task AsyncAllTheWay()
    {
        await DoThisAsync();
    }

    // ❌ WRONG: Not awaiting in loops
    public async Task FireAndForgetWrong()
    {
        var tasks = new List<Task>();

        for (int i = 0; i < 5; i++)
        {
            // Starts all tasks but doesn't wait
            DoThisAsync(); // Fire and forget - BAD!
        }

        // Tasks may not be complete here
    }

    // ✅ CORRECT: Collect and await
    public async Task CollectAndAwaitCorrect()
    {
        var tasks = new List<Task>();

        for (int i = 0; i < 5; i++)
        {
            tasks.Add(DoThisAsync());
        }

        await Task.WhenAll(tasks);
        // All tasks guaranteed complete
    }

    // ❌ WRONG: Using Task.Run for I/O
    public async Task<string> TaskRunForIOWrong()
    {
        return await Task.Run(async () =>
        {
            // Waste of thread - async I/O doesn't need thread
            return await File.ReadAllTextAsync("file.txt");
        });
    }

    // ✅ CORRECT: Direct async I/O
    public async Task<string> AsyncIOCorrect()
    {
        return await File.ReadAllTextAsync("file.txt");
    }
}
```

---

## Part 7: Real-World Example

### Step 7.1: Build an Async Data Processor

**Your Task:** Create `Examples/DataProcessor.cs`:

```csharp
namespace AsyncAwait.Examples;

public class DataProcessor
{
    private readonly HttpClient _httpClient = new();

    public async Task<List<string>> ProcessUrlsAsync(
        List<string> urls,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Processing {urls.Count} URLs...\n");

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

            // Simulate HTTP call
            await Task.Delay(Random.Shared.Next(500, 2000), cancellationToken);

            Console.WriteLine($"[Processing] {url}");

            // Simulate processing
            await Task.Delay(500, cancellationToken);

            Console.WriteLine($"[Complete] {url}");

            return $"Processed: {url}";
        }
        catch (OperationCanceledException)
        {
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
            Console.WriteLine($"\n✅ Processed {results.Count} URLs successfully");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n❌ Processing timed out!");
        }
    }
}
```

---

## Challenge Exercise

### Build an Async Download Manager

**Requirements:**

1. **Download multiple files concurrently**
2. **Show progress for each download**
3. **Support cancellation**
4. **Handle failures gracefully**
5. **Retry failed downloads (max 3 attempts)**
6. **Timeout individual downloads (10 seconds)**
7. **Report overall statistics**

**Hints:**
- Use `HttpClient` for downloads
- Use `IProgress<T>` for progress reporting
- Combine `CancellationToken` with timeout
- Use `Task.WhenAll` for concurrency
- Track success/failure counts

---

## Summary

You've learned:
- ✅ Async/await fundamentals
- ✅ Task.WhenAll and Task.WhenAny
- ✅ Cancellation with CancellationToken
- ✅ Exception handling in async code
- ✅ ConfigureAwait usage
- ✅ Common pitfalls and how to avoid them

**Next:** Move to [TaskParallelLibrary](../TaskParallelLibrary/) for CPU-bound parallelism!
