# Exercise: Task Parallel Library (TPL)

## Overview
Learn to parallelize CPU-bound operations using the Task Parallel Library. Master techniques for processing large datasets efficiently using all available CPU cores.

## Learning Goals
- Use Parallel.For and Parallel.ForEach
- Understand PLINQ (Parallel LINQ)
- Control degree of parallelism
- Handle exceptions in parallel operations
- Partition work effectively
- Know when to use parallel vs async

---

## Part 1: Parallel.For and Parallel.ForEach

### Step 1.1: Basic Parallel.For

**Your Task:** Create `Examples/BasicParallel.cs`:

```csharp
namespace TaskParallelLibrary.Examples;

public class BasicParallel
{
    private long ComputeFactorial(int n)
    {
        long result = 1;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
        }
        return result;
    }

    public void SequentialProcessing()
    {
        Console.WriteLine("=== SEQUENTIAL PROCESSING ===\n");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
        {
            var result = ComputeFactorial(20);
            Console.Write($"{i} ");
        }

        sw.Stop();
        Console.WriteLine($"\n\nTime: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Thread used: Single thread");
    }

    public void ParallelProcessing()
    {
        Console.WriteLine("\n=== PARALLEL PROCESSING ===\n");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var threadIds = new System.Collections.Concurrent.ConcurrentBag<int>();

        Parallel.For(0, 100, i =>
        {
            threadIds.Add(Thread.CurrentThread.ManagedThreadId);
            var result = ComputeFactorial(20);
            Console.Write($"{i} ");
        });

        sw.Stop();
        Console.WriteLine($"\n\nTime: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Threads used: {threadIds.Distinct().Count()}");
        Console.WriteLine($"Speedup: {(double)sw.ElapsedMilliseconds / sw.ElapsedMilliseconds:F2}x");
    }
}
```

**Observe:**
- Parallel.For uses multiple threads
- Faster completion time
- Order of console output may vary

### Step 1.2: Parallel.ForEach

**Your Task:** Create `Examples/ParallelForEach.cs`:

```csharp
namespace TaskParallelLibrary.Examples;

public class ParallelForEachExample
{
    public class Product
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockLevel { get; set; }
    }

    private List<Product> GenerateProducts(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new Product
            {
                Name = $"Product {i}",
                Price = Random.Shared.Next(10, 1000),
                StockLevel = Random.Shared.Next(0, 100)
            })
            .ToList();
    }

    private void ProcessProduct(Product product)
    {
        // Simulate heavy processing
        Thread.Sleep(10);

        // Apply business logic
        if (product.StockLevel < 10)
        {
            product.Price *= 0.9m; // 10% discount for low stock
        }
    }

    public void SequentialForEach()
    {
        Console.WriteLine("=== SEQUENTIAL FOREACH ===\n");

        var products = GenerateProducts(1000);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var product in products)
        {
            ProcessProduct(product);
        }

        sw.Stop();
        Console.WriteLine($"Processed {products.Count} products");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
    }

    public void ParallelForEach()
    {
        Console.WriteLine("\n=== PARALLEL FOREACH ===\n");

        var products = GenerateProducts(1000);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Parallel.ForEach(products, product =>
        {
            ProcessProduct(product);
        });

        sw.Stop();
        Console.WriteLine($"Processed {products.Count} products");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Speedup: ~{Environment.ProcessorCount}x (approx)");
    }
}
```

---

## Part 2: PLINQ (Parallel LINQ)

### Step 2.1: AsParallel Basics

**Your Task:** Create `Examples/PlinqExample.cs`:

```csharp
namespace TaskParallelLibrary.Examples;

public class PlinqExample
{
    private bool IsPrime(int number)
    {
        if (number < 2) return false;
        if (number == 2) return true;
        if (number % 2 == 0) return false;

        int limit = (int)Math.Sqrt(number);
        for (int i = 3; i <= limit; i += 2)
        {
            if (number % i == 0) return false;
        }
        return true;
    }

    public void SequentialLinq()
    {
        Console.WriteLine("=== SEQUENTIAL LINQ ===\n");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var primes = Enumerable.Range(1, 100_000)
            .Where(IsPrime)
            .ToList();

        sw.Stop();
        Console.WriteLine($"Found {primes.Count} prime numbers");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
    }

    public void ParallelLinq()
    {
        Console.WriteLine("\n=== PARALLEL LINQ (PLINQ) ===\n");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var primes = Enumerable.Range(1, 100_000)
            .AsParallel()  // Enable parallelism
            .Where(IsPrime)
            .ToList();

        sw.Stop();
        Console.WriteLine($"Found {primes.Count} prime numbers");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
    }

    public void PlinqWithOrdering()
    {
        Console.WriteLine("\n=== PLINQ WITH ORDERING ===\n");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // AsOrdered() maintains original order (slower)
        var primes = Enumerable.Range(1, 100_000)
            .AsParallel()
            .AsOrdered()  // Maintain order
            .Where(IsPrime)
            .ToList();

        sw.Stop();
        Console.WriteLine($"Found {primes.Count} prime numbers (ordered)");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"First 10: {string.Join(", ", primes.Take(10))}");
    }
}
```

**Key Methods:**
- `.AsParallel()`: Enable parallel execution
- `.AsOrdered()`: Maintain source order
- `.WithDegreeOfParallelism(n)`: Limit threads
- `.WithCancellation(token)`: Support cancellation

---

## Part 3: Controlling Parallelism

### Step 3.1: Degree of Parallelism

**Your Task:** Create `Examples/ParallelismControl.cs`:

```csharp
namespace TaskParallelLibrary.Examples;

public class ParallelismControl
{
    private void ExpensiveOperation(int id)
    {
        Thread.Sleep(100);
    }

    public void DefaultParallelism()
    {
        Console.WriteLine("=== DEFAULT PARALLELISM ===\n");
        Console.WriteLine($"Processor count: {Environment.ProcessorCount}");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Uses all available cores by default
        Parallel.For(0, 20, i =>
        {
            Console.WriteLine($"Task {i} on thread {Thread.CurrentThread.ManagedThreadId}");
            ExpensiveOperation(i);
        });

        sw.Stop();
        Console.WriteLine($"\nTime: {sw.ElapsedMilliseconds}ms");
    }

    public void LimitedParallelism()
    {
        Console.WriteLine("\n=== LIMITED PARALLELISM (Max 2 threads) ===\n");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 2  // Limit to 2 threads
        };

        Parallel.For(0, 20, options, i =>
        {
            Console.WriteLine($"Task {i} on thread {Thread.CurrentThread.ManagedThreadId}");
            ExpensiveOperation(i);
        });

        sw.Stop();
        Console.WriteLine($"\nTime: {sw.ElapsedMilliseconds}ms");
    }

    public void PlinqDegreeOfParallelism()
    {
        Console.WriteLine("\n=== PLINQ WITH CUSTOM PARALLELISM ===\n");

        var results = Enumerable.Range(1, 100)
            .AsParallel()
            .WithDegreeOfParallelism(4)  // Use exactly 4 threads
            .Select(i =>
            {
                Thread.Sleep(10);
                return i * i;
            })
            .ToList();

        Console.WriteLine($"Processed {results.Count} items");
    }
}
```

**When to limit parallelism:**
- Resource-intensive operations (database connections)
- External API rate limits
- Memory constraints
- Avoiding thread pool starvation

---

## Part 4: Exception Handling

### Step 4.1: Handling Parallel Exceptions

**Your Task:** Create `Examples/ParallelExceptions.cs`:

```csharp
namespace TaskParallelLibrary.Examples;

public class ParallelExceptionHandling
{
    private void ProcessItem(int item)
    {
        if (item == 5 || item == 15)
        {
            throw new InvalidOperationException($"Failed to process item {item}");
        }

        Thread.Sleep(100);
        Console.WriteLine($"Processed item {item}");
    }

    public void HandleParallelExceptions()
    {
        Console.WriteLine("=== PARALLEL EXCEPTION HANDLING ===\n");

        try
        {
            Parallel.For(0, 20, i =>
            {
                ProcessItem(i);
            });
        }
        catch (AggregateException ae)
        {
            Console.WriteLine($"\n❌ Caught {ae.InnerExceptions.Count} exceptions:\n");

            foreach (var ex in ae.InnerExceptions)
            {
                Console.WriteLine($"  - {ex.Message}");
            }
        }
    }

    public void HandlePlinqExceptions()
    {
        Console.WriteLine("\n=== PLINQ EXCEPTION HANDLING ===\n");

        try
        {
            var results = Enumerable.Range(0, 20)
                .AsParallel()
                .Select(i =>
                {
                    ProcessItem(i);
                    return i;
                })
                .ToList();
        }
        catch (AggregateException ae)
        {
            Console.WriteLine($"❌ PLINQ threw {ae.InnerExceptions.Count} exceptions");

            ae.Handle(ex =>
            {
                if (ex is InvalidOperationException)
                {
                    Console.WriteLine($"  Handled: {ex.Message}");
                    return true; // Exception handled
                }
                return false; // Rethrow unhandled
            });
        }
    }
}
```

**Key Points:**
- Parallel operations throw `AggregateException`
- Contains all exceptions from parallel tasks
- Use `.Handle()` to selectively handle exceptions

---

## Part 5: Cancellation

### Step 5.1: Cancelling Parallel Operations

**Your Task:** Create `Examples/ParallelCancellation.cs`:

```csharp
namespace TaskParallelLibrary.Examples;

public class ParallelCancellation
{
    private void ProcessItem(int item, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        Thread.Sleep(200);
        Console.WriteLine($"Processed {item}");
    }

    public void DemonstrateCancellation()
    {
        Console.WriteLine("=== PARALLEL CANCELLATION ===\n");

        using var cts = new CancellationTokenSource();

        // Cancel after 2 seconds
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        var options = new ParallelOptions
        {
            CancellationToken = cts.Token
        };

        try
        {
            Parallel.For(0, 50, options, i =>
            {
                ProcessItem(i, cts.Token);
            });

            Console.WriteLine("\n✅ All items processed");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n❌ Operation cancelled!");
        }
    }

    public void PlinqCancellation()
    {
        Console.WriteLine("\n=== PLINQ CANCELLATION ===\n");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            var results = Enumerable.Range(0, 50)
                .AsParallel()
                .WithCancellation(cts.Token)
                .Select(i =>
                {
                    Thread.Sleep(200);
                    Console.WriteLine($"Processed {i}");
                    return i;
                })
                .ToList();

            Console.WriteLine($"\n✅ Processed {results.Count} items");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n❌ PLINQ cancelled!");
        }
    }
}
```

---

## Part 6: Partitioning Strategies

### Step 6.1: Custom Partitioning

**Your Task:** Create `Examples/Partitioning.cs`:

```csharp
namespace TaskParallelLibrary.Examples;

public class PartitioningExample
{
    public void DefaultPartitioning()
    {
        Console.WriteLine("=== DEFAULT PARTITIONING ===\n");

        Parallel.ForEach(
            Partitioner.Create(0, 100),
            range =>
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} " +
                                $"processing range [{range.Item1}, {range.Item2})");

                for (int i = range.Item1; i < range.Item2; i++)
                {
                    // Process item
                    Thread.Sleep(10);
                }
            });
    }

    public void CustomPartitioning()
    {
        Console.WriteLine("\n=== CUSTOM CHUNK SIZE ===\n");

        // Process in chunks of 10
        Parallel.ForEach(
            Partitioner.Create(0, 100, 10),  // Chunk size = 10
            range =>
            {
                Console.WriteLine($"Processing chunk [{range.Item1}, {range.Item2})");

                for (int i = range.Item1; i < range.Item2; i++)
                {
                    Thread.Sleep(10);
                }
            });
    }
}
```

---

## Part 7: Async vs Parallel

### Step 7.1: When to Use Which

**Your Task:** Create `Examples/AsyncVsParallel.cs`:

```csharp
namespace TaskParallelLibrary.Examples;

public class AsyncVsParallel
{
    // ✅ Use ASYNC for I/O-bound operations
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

    private async Task<string> DownloadAsync(string url)
    {
        await Task.Delay(1000); // Simulating I/O
        return "data";
    }

    // ✅ Use PARALLEL for CPU-bound operations
    public void CPUBoundOperation()
    {
        var numbers = Enumerable.Range(1, 1000000).ToArray();

        Parallel.ForEach(numbers, number =>
        {
            // Heavy CPU work
            double result = Math.Sqrt(number) * Math.Log(number);
        });
    }

    // ❌ WRONG: Parallel for I/O
    public void WrongParallelForIO()
    {
        Parallel.For(0, 10, i =>
        {
            // Blocks thread waiting for I/O - wastes thread
            Thread.Sleep(1000);
        });
    }

    // ❌ WRONG: Task.Run for every CPU operation
    public async Task WrongAsyncForCPU()
    {
        var tasks = Enumerable.Range(1, 100)
            .Select(i => Task.Run(() => Math.Sqrt(i)))
            .ToArray();

        await Task.WhenAll(tasks);
        // Overhead of task scheduling - use Parallel instead
    }
}
```

**Decision Matrix:**
| Operation | Use | Why |
|-----------|-----|-----|
| File I/O | Async | Doesn't block thread |
| HTTP calls | Async | Network I/O |
| Database queries | Async | I/O operation |
| Image processing | Parallel | CPU-intensive |
| Data transformation | Parallel | CPU-bound |
| Calculations | Parallel | Uses CPU |

---

## Challenge Exercise

### Build a Parallel Image Processor

**Requirements:**

1. **Process 100 simulated images in parallel**
2. **Each image takes different processing time**
3. **Track progress (completed count)**
4. **Support cancellation**
5. **Handle failures (some images fail)**
6. **Report statistics:**
   - Total time
   - Successful/failed counts
   - Average processing time
   - Threads used

**Hints:**
- Use `Parallel.ForEach` or PLINQ
- Use `ConcurrentBag` for thread-safe collections
- Use `Interlocked.Increment` for atomic counters
- Add `CancellationToken` support
- Measure individual processing times

---

## Summary

You've learned:
- ✅ Parallel.For and Parallel.ForEach
- ✅ PLINQ for parallel LINQ queries
- ✅ Controlling degree of parallelism
- ✅ Exception handling in parallel code
- ✅ Cancellation support
- ✅ Partitioning strategies
- ✅ When to use Async vs Parallel

**Next:** Move to [Channels](../Channels/) for producer-consumer patterns!
