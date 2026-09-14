namespace AsyncAwait.Tests;

/// <summary>
/// Part 2 and Part 4: the rules of WhenAll / WhenAny that are easy to state and
/// easy to get wrong. None of these tests sleep -- they use completion sources
/// so the ordering is exact rather than probable.
/// </summary>
public class TaskCombinatorTests
{
    [Fact]
    public async Task WhenAll_returns_results_in_argument_order_not_completion_order()
    {
        var slow = new TaskCompletionSource<int>();
        var fast = new TaskCompletionSource<int>();

        var all = Task.WhenAll(slow.Task, fast.Task);

        fast.SetResult(2);   // finishes first
        slow.SetResult(1);   // finishes second

        var results = await all;

        // Order follows the arguments, not the clock.
        results.Should().Equal(new[] { 1, 2 });
    }

    [Fact]
    public async Task WhenAny_completes_as_soon_as_one_task_does()
    {
        var slow = new TaskCompletionSource<string>();
        var fast = new TaskCompletionSource<string>();

        var any = Task.WhenAny(slow.Task, fast.Task);
        fast.SetResult("fast");

        var winner = await any;

        (await winner).Should().Be("fast");
        slow.Task.IsCompleted.Should().BeFalse("the loser keeps running");

        slow.SetResult("slow"); // tidy up so nothing is left dangling
    }

    [Fact]
    public async Task Awaiting_WhenAll_surfaces_only_the_first_exception()
    {
        var first = Task.FromException<int>(new InvalidOperationException("first"));
        var second = Task.FromException<int>(new InvalidOperationException("second"));

        var all = Task.WhenAll(first, second);

        var act = async () => await all;

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Be("first");
    }

    [Fact]
    public async Task But_every_exception_is_still_available_on_the_WhenAll_task()
    {
        // This is the practical follow-up to the test above: awaiting hides the
        // rest, reading .Exception does not.
        var all = Task.WhenAll(
            Task.FromException<int>(new InvalidOperationException("first")),
            Task.FromException<int>(new InvalidOperationException("second")));

        try { await all; } catch (InvalidOperationException) { /* expected */ }

        all.Exception!.InnerExceptions
            .Select(e => e.Message)
            .Should().BeEquivalentTo(["first", "second"]);
    }

    [Fact]
    public async Task Starting_tasks_before_awaiting_them_is_what_creates_overlap()
    {
        var started = 0;

        Task<int> Work()
        {
            Interlocked.Increment(ref started);
            return Task.FromResult(1);
        }

        // Both are started here...
        var a = Work();
        var b = Work();
        started.Should().Be(2, "both tasks began before either was awaited");

        await Task.WhenAll(a, b);
    }
}
