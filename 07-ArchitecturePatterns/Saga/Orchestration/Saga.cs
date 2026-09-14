namespace Saga.Orchestration;

/// <summary>
/// One step of a distributed transaction, and the compensation that undoes it.
///
/// There is no two-phase commit across services, so "roll back" means "do
/// something that cancels out what you already did". Refunding a payment is not
/// the same as never taking it -- the customer saw both -- and a saga is the
/// acknowledgement that this is the best available.
/// </summary>
public interface ISagaStep<TContext>
{
    string Name { get; }
    Task ExecuteAsync(TContext context, CancellationToken cancellationToken);
    Task CompensateAsync(TContext context, CancellationToken cancellationToken);
}

public record SagaResult(
    bool Succeeded,
    IReadOnlyList<string> Completed,
    IReadOnlyList<string> Compensated,
    string? FailedStep,
    string? Error);

/// <summary>
/// ORCHESTRATION: one coordinator runs the steps and, on failure, compensates
/// the ones that already succeeded -- in reverse order.
///
/// Reverse order matters. If step 3 depended on step 2, undoing 2 before 3
/// leaves the system in a state neither step expected.
/// </summary>
public class SagaOrchestrator<TContext>
{
    private readonly IReadOnlyList<ISagaStep<TContext>> _steps;
    private readonly Action<string>? _log;

    public SagaOrchestrator(IReadOnlyList<ISagaStep<TContext>> steps, Action<string>? log = null)
    {
        _steps = steps;
        _log = log;
    }

    public async Task<SagaResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        var completed = new List<ISagaStep<TContext>>();

        foreach (var step in _steps)
        {
            try
            {
                _log?.Invoke($"  -> {step.Name}");
                await step.ExecuteAsync(context, cancellationToken);
                completed.Add(step);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"  !! {step.Name} failed: {ex.Message}");

                var compensated = await CompensateAsync(completed, context, cancellationToken);

                return new SagaResult(false, completed.Select(s => s.Name).ToArray(),
                    compensated, step.Name, ex.Message);
            }
        }

        return new SagaResult(true, completed.Select(s => s.Name).ToArray(), [], null, null);
    }

    private async Task<IReadOnlyList<string>> CompensateAsync(
        List<ISagaStep<TContext>> completed, TContext context, CancellationToken cancellationToken)
    {
        var compensated = new List<string>();

        // Reverse order: undo the most recent step first.
        foreach (var step in Enumerable.Reverse(completed))
        {
            try
            {
                _log?.Invoke($"  <- compensating {step.Name}");
                await step.CompensateAsync(context, cancellationToken);
                compensated.Add(step.Name);
            }
            catch (Exception ex)
            {
                // A failed compensation is the worst case in a saga: the system
                // is now inconsistent and cannot fix itself. Real systems retry
                // this forever and page a human if it keeps failing. Swallowing
                // it here would hide the one thing that must never be hidden.
                _log?.Invoke($"  XX compensation for {step.Name} FAILED: {ex.Message}");
            }
        }

        return compensated;
    }
}
