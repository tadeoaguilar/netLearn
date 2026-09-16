using Microsoft.Azure.Cosmos;

namespace CosmosIndexingAndThroughput.Throughput;

// Manual throughput fixes RU/s (predictable bill, hard cap -- traffic above
// it gets throttled). Autoscale sets a MAX RU/s and the service scales
// between 10% and 100% of that max automatically based on usage (variable
// bill, absorbs bursts). Both are provisioned here via the client SDK; see
// the README for why the AppHost doesn't do this instead.
public static class ThroughputWalkthrough
{
    public const string ManualContainerName = "orders-manual";
    public const string AutoscaleContainerName = "orders-autoscale";
    private const int ManualThroughputRUs = 400;
    private const int AutoscaleMaxThroughputRUs = 4000;

    public static async Task RunAsync(Database database)
    {
        Console.WriteLine("=== Part 4: Manual vs. autoscale throughput ===");

        var manualProperties = new ContainerProperties(ManualContainerName, "/customerId");
        var manualContainerResponse = await database.CreateContainerIfNotExistsAsync(
            manualProperties, ThroughputProperties.CreateManualThroughput(ManualThroughputRUs));
        var manualThroughput = await manualContainerResponse.Container.ReadThroughputAsync();
        Console.WriteLine($"{ManualContainerName}: manual, fixed at {manualThroughput} RU/s -- predictable cost, no scaling.");

        var autoscaleProperties = new ContainerProperties(AutoscaleContainerName, "/customerId");
        var autoscaleContainerResponse = await database.CreateContainerIfNotExistsAsync(
            autoscaleProperties, ThroughputProperties.CreateAutoscaleThroughput(AutoscaleMaxThroughputRUs));
        var autoscaleThroughputResponse = await autoscaleContainerResponse.Container.ReadThroughputAsync(new RequestOptions());
        var maxRUs = autoscaleThroughputResponse.Resource.AutoscaleMaxThroughput;
        Console.WriteLine($"{AutoscaleContainerName}: autoscale, max {maxRUs} RU/s, scales down to {maxRUs / 10} RU/s (10%) automatically when idle.");

        Console.WriteLine("Manual throughput is cheaper to reason about and bills a flat rate; autoscale costs up to 1.5x");
        Console.WriteLine("the scaled-to RU/s but absorbs traffic spikes without provisioning for worst case up front.");
        Console.WriteLine("Note: the Cosmos emulator accepts both throughput APIs but does not meaningfully enforce RU/s");
        Console.WriteLine("limits or scale the way the real service does -- treat this as verifying the SDK calls are");
        Console.WriteLine("correct, not as a real load test.");
        Console.WriteLine();
    }
}
