using System.Diagnostics;

namespace TaskParallelLibrary.Examples;

/// <summary>Part 1.2: Parallel.ForEach over a collection.</summary>
public class ParallelForEachExample
{
    public class Product
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockLevel { get; set; }
    }

    private static List<Product> GenerateProducts(int count)
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

    private static void ProcessProduct(Product product)
    {
        Thread.Sleep(10); // Simulate heavy processing

        if (product.StockLevel < 10)
        {
            product.Price *= 0.9m; // 10% discount for low stock
        }
    }

    public void SequentialForEach()
    {
        Console.WriteLine("=== SEQUENTIAL FOREACH ===\n");

        var products = GenerateProducts(200);
        var sw = Stopwatch.StartNew();

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

        var products = GenerateProducts(200);
        var sw = Stopwatch.StartNew();

        // Each product is touched by exactly one iteration, so no locking is
        // needed. Mutating shared state here would be a data race.
        Parallel.ForEach(products, ProcessProduct);

        sw.Stop();
        Console.WriteLine($"Processed {products.Count} products");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"\nThis workload is Thread.Sleep -- it is fake I/O, not CPU work.");
        Console.WriteLine($"Parallel.ForEach speeds it up, but async would do it without");
        Console.WriteLine($"burning {Environment.ProcessorCount} threads on waiting. See Part 7.");
    }
}
