using EventSourcing.Domain;
using EventSourcing.Projections;
using EventSourcing.Store;

namespace AdvancedTopics.Tests;

public class EventSourcingTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static (InMemoryEventStore Store, string Id) Seeded()
    {
        var store = new InMemoryEventStore();
        var account = BankAccount.Open("acct-1", "Ada", Now);
        account.Deposit(1000m, Now.AddMinutes(1));
        account.Withdraw(250m, Now.AddMinutes(2));
        store.Append("acct-1", 0, account.UncommittedEvents);
        return (store, "acct-1");
    }

    [Fact]
    public void State_is_rebuilt_from_events_not_stored()
    {
        var (store, id) = Seeded();

        var account = BankAccount.Rehydrate(store.Read(id));

        account.Balance.Should().Be(750m);
        account.Owner.Should().Be("Ada");
        account.Version.Should().Be(3);
    }

    [Fact]
    public void Replaying_a_prefix_gives_the_state_at_that_point_in_time()
    {
        // Time travel for free, because history is the source of truth.
        var (store, id) = Seeded();
        var all = store.Read(id);

        BankAccount.Rehydrate(all.Take(1)).Balance.Should().Be(0m);
        BankAccount.Rehydrate(all.Take(2)).Balance.Should().Be(1000m);
        BankAccount.Rehydrate(all.Take(3)).Balance.Should().Be(750m);
    }

    [Fact]
    public void A_rejected_command_appends_no_event()
    {
        var (store, id) = Seeded();
        var account = BankAccount.Rehydrate(store.Read(id));
        var before = store.TotalEvents;

        var act = () => account.Withdraw(10_000m, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Insufficient funds*");
        account.UncommittedEvents.Should().BeEmpty();
        store.TotalEvents.Should().Be(before);
    }

    [Fact]
    public void A_snapshot_plus_later_events_equals_a_full_replay()
    {
        var (store, id) = Seeded();

        var full = BankAccount.Rehydrate(store.Read(id));
        var snapshot = full.TakeSnapshot();

        full.Deposit(500m, Now.AddMinutes(3));
        store.Append(id, full.Version, full.UncommittedEvents);

        var fromScratch = BankAccount.Rehydrate(store.Read(id));
        var fromSnapshot = BankAccount.Rehydrate(snapshot, store.Read(id, snapshot.Version));

        fromSnapshot.Balance.Should().Be(fromScratch.Balance);
        fromSnapshot.Version.Should().Be(fromScratch.Version);
    }

    [Fact]
    public void Discarding_every_snapshot_changes_nothing_but_speed()
    {
        // A snapshot is a cache of a fold, never a source of truth.
        var (store, id) = Seeded();

        var withSnapshot = BankAccount.Rehydrate(BankAccount.Rehydrate(store.Read(id)).TakeSnapshot(), []);
        var withoutSnapshot = BankAccount.Rehydrate(store.Read(id));

        withSnapshot.Balance.Should().Be(withoutSnapshot.Balance);
    }

    [Fact]
    public void A_stale_writer_is_rejected()
    {
        var (store, id) = Seeded();

        var userA = BankAccount.Rehydrate(store.Read(id));
        var userB = BankAccount.Rehydrate(store.Read(id));

        userA.Withdraw(100m, Now);
        store.Append(id, userA.Version, userA.UncommittedEvents);

        userB.Withdraw(700m, Now);
        var act = () => store.Append(id, userB.Version, userB.UncommittedEvents);

        act.Should().Throw<ConcurrencyException>().WithMessage("*expected 3*");
    }

    [Fact]
    public void Without_the_concurrency_check_the_account_would_overdraw()
    {
        // Why the check matters: both writers decided against the same balance.
        var (store, id) = Seeded();

        var userA = BankAccount.Rehydrate(store.Read(id));
        var userB = BankAccount.Rehydrate(store.Read(id));

        userA.Balance.Should().Be(750m);
        userB.Balance.Should().Be(750m);

        userA.Withdraw(700m, Now);
        userB.Withdraw(700m, Now);

        store.Append(id, userA.Version, userA.UncommittedEvents);
        FluentActions.Invoking(() => store.Append(id, userB.Version, userB.UncommittedEvents))
            .Should().Throw<ConcurrencyException>();

        BankAccount.Rehydrate(store.Read(id)).Balance.Should().Be(50m, "only one withdrawal applied");
    }

    [Fact]
    public void A_projection_written_later_can_answer_questions_about_older_events()
    {
        // The strongest argument for event sourcing: information is not thrown
        // away at write time, so a report invented today works retroactively.
        var (store, id) = Seeded();

        var report = new LargeTransactionReport(threshold: 500m);
        foreach (var stored in store.Read(id)) report.Project(stored);

        report.Entries.Should().ContainSingle()
            .Which.Amount.Should().Be(1000m);
    }

    [Fact]
    public void A_statement_projection_tracks_a_running_balance()
    {
        var (store, id) = Seeded();

        var statement = new AccountStatement();
        foreach (var stored in store.Read(id)) statement.Project(stored);

        statement.Lines.Select(l => l.RunningBalance).Should().Equal(0m, 1000m, 750m);
        statement.Balance.Should().Be(750m);
    }

    [Fact]
    public void Freezing_is_idempotent()
    {
        var account = BankAccount.Open("acct-2", "Grace", Now);
        account.MarkCommitted();

        account.Freeze("fraud review", Now);
        account.Freeze("fraud review", Now);

        account.UncommittedEvents.Should().ContainSingle("freezing an already-frozen account is a no-op");
        account.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public void A_frozen_account_refuses_transactions()
    {
        var account = BankAccount.Open("acct-3", "Linus", Now);
        account.Deposit(100m, Now);
        account.Freeze("fraud review", Now);

        FluentActions.Invoking(() => account.Deposit(10m, Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => account.Withdraw(10m, Now)).Should().Throw<InvalidOperationException>();

        account.Unfreeze(Now);
        FluentActions.Invoking(() => account.Deposit(10m, Now)).Should().NotThrow();
    }
}
