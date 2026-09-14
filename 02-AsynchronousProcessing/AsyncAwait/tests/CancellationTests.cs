using AsyncAwait.Examples;

namespace AsyncAwait.Tests;

/// <summary>Part 3: cancellation is cooperative, and the token has to be passed on.</summary>
public class CancellationTests
{
    [Fact]
    public async Task An_already_cancelled_token_stops_the_operation_immediately()
    {
        var sut = new CancellationExample();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => sut.LongRunningOperationAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Cancelling_mid_flight_stops_the_loop_before_it_finishes()
    {
        var sut = new CancellationExample();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var act = () => sut.LongRunningOperationAsync(cts.Token);

        // The operation would take 5s if it ran all ten steps.
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task A_token_passed_to_Task_Delay_cancels_the_wait_itself()
    {
        using var cts = new CancellationTokenSource();
        var delay = Task.Delay(TimeSpan.FromMinutes(5), cts.Token);

        await cts.CancelAsync();

        var act = async () => await delay;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Without_the_token_the_wait_runs_to_completion_regardless()
    {
        // The counterpart to the test above -- forgetting to pass the token is
        // why "cancellation does nothing" bug reports happen.
        using var cts = new CancellationTokenSource();
        var delay = Task.Delay(TimeSpan.FromMilliseconds(50)); // no token

        await cts.CancelAsync();
        await delay;

        delay.IsCanceled.Should().BeFalse();
        delay.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task A_linked_source_is_cancelled_by_either_parent()
    {
        using var caller = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource();
        using var linked = CancellationTokenSource
            .CreateLinkedTokenSource(caller.Token, timeout.Token);

        await timeout.CancelAsync();

        linked.Token.IsCancellationRequested.Should().BeTrue();
        caller.Token.IsCancellationRequested.Should().BeFalse(
            "which is how the download manager tells a timeout from a real cancel");
    }
}
