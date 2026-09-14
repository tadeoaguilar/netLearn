using CQRS.Read;
using CQRS.Write;

namespace DistributedSystems.Tests;

public class CqrsTests
{
    private static (WriteStore Write, ReadStore Read) Build()
    {
        var write = new WriteStore();
        var read = new ReadStore();
        read.Seed("SKU-1", "Keyboard");
        write.Add(new Product("SKU-1", "Keyboard", 100m, 25));
        return (write, read);
    }

    private static void Sync(WriteStore write, ReadStore read)
    {
        foreach (var e in write.DrainEvents()) read.Project(e);
    }

    [Fact]
    public void A_projection_precomputes_what_a_query_would_have_to_derive()
    {
        var (write, read) = Build();
        Sync(write, read);

        read.Get("SKU-1")!.Availability.Should().Be("In stock");

        write.Reserve("SKU-1", 20);   // 25 -> 5
        Sync(write, read);

        read.Get("SKU-1")!.Availability.Should().Be("Low stock");

        write.Reserve("SKU-1", 5);    // 5 -> 0
        Sync(write, read);

        read.Get("SKU-1")!.Availability.Should().Be("Out of stock");
    }

    [Fact]
    public void The_read_model_is_stale_until_the_projection_runs()
    {
        // The defining property of CQRS, asserted rather than hand-waved.
        var (write, read) = Build();
        Sync(write, read);

        write.Reserve("SKU-1", 20);

        write.Get("SKU-1").Stock.Should().Be(5);
        read.Get("SKU-1")!.Stock.Should().Be(25, "the projection has not run yet");

        Sync(write, read);

        read.Get("SKU-1")!.Stock.Should().Be(5, "and now it has");
    }

    [Fact]
    public void A_rejected_command_emits_no_event_and_leaves_the_read_model_untouched()
    {
        var (write, read) = Build();
        Sync(write, read);
        var before = read.ProjectedEvents;

        var act = () => write.Reserve("SKU-1", 999);

        act.Should().Throw<InvalidOperationException>();
        Sync(write, read);
        read.ProjectedEvents.Should().Be(before);
        read.Get("SKU-1")!.Stock.Should().Be(25);
    }

    [Fact]
    public void Business_rules_are_enforced_on_the_write_side_only()
    {
        var (write, _) = Build();

        FluentActions.Invoking(() => write.Reprice("SKU-1", -5m))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => write.Reserve("SKU-1", 26))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Replaying_every_event_rebuilds_the_read_model_from_scratch()
    {
        // Because the read model is derived, it can always be thrown away and
        // rebuilt -- which is how you fix a projection bug in production.
        var write = new WriteStore();
        write.Add(new Product("SKU-1", "Keyboard", 100m, 25));
        write.Reprice("SKU-1", 80m);
        write.Reserve("SKU-1", 20);

        var events = write.DrainEvents();

        var rebuilt = new ReadStore();
        rebuilt.Seed("SKU-1", "Keyboard");
        foreach (var e in events) rebuilt.Project(e);

        var item = rebuilt.Get("SKU-1")!;
        item.Price.Should().Be(80m);
        item.Stock.Should().Be(5);
        item.Availability.Should().Be("Low stock");
    }
}
