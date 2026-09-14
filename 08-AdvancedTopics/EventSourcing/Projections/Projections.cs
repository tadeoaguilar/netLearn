using EventSourcing.Domain;
using EventSourcing.Store;

namespace EventSourcing.Projections;

/// <summary>
/// A read model built by replaying events.
///
/// Because it is derived, it can be deleted and rebuilt at any time -- which is
/// how you fix a projection bug in production, and how you add a report that
/// nobody thought of when the events were written.
/// </summary>
public class AccountStatement
{
    public record Line(long Version, string Description, decimal Amount, decimal RunningBalance);

    private readonly List<Line> _lines = new();

    public IReadOnlyList<Line> Lines => _lines;
    public decimal Balance => _lines.Count > 0 ? _lines[^1].RunningBalance : 0m;

    public void Project(StoredEvent stored)
    {
        var balance = Balance;

        switch (stored.Event)
        {
            case AccountOpened opened:
                _lines.Add(new Line(stored.Version, $"Account opened for {opened.Owner}", 0m, 0m));
                break;
            case MoneyDeposited deposited:
                _lines.Add(new Line(stored.Version, "Deposit", deposited.Amount, balance + deposited.Amount));
                break;
            case MoneyWithdrawn withdrawn:
                _lines.Add(new Line(stored.Version, "Withdrawal", -withdrawn.Amount, balance - withdrawn.Amount));
                break;
            case AccountFrozen frozen:
                _lines.Add(new Line(stored.Version, $"Frozen: {frozen.Reason}", 0m, balance));
                break;
            case AccountUnfrozen:
                _lines.Add(new Line(stored.Version, "Unfrozen", 0m, balance));
                break;
        }
    }
}

/// <summary>
/// A projection written LATER, answering a question nobody asked when the
/// events were recorded.
///
/// This is the single strongest argument for event sourcing: a system storing
/// only current balances could never produce this report retroactively, because
/// the information was thrown away at write time.
/// </summary>
public class LargeTransactionReport
{
    public record Entry(long Version, string Kind, decimal Amount, DateTimeOffset At);

    private readonly List<Entry> _entries = new();
    private readonly decimal _threshold;

    public LargeTransactionReport(decimal threshold) => _threshold = threshold;

    public IReadOnlyList<Entry> Entries => _entries;

    public void Project(StoredEvent stored)
    {
        switch (stored.Event)
        {
            case MoneyDeposited d when d.Amount >= _threshold:
                _entries.Add(new Entry(stored.Version, "Deposit", d.Amount, d.OccurredAt));
                break;
            case MoneyWithdrawn w when w.Amount >= _threshold:
                _entries.Add(new Entry(stored.Version, "Withdrawal", w.Amount, w.OccurredAt));
                break;
        }
    }
}
