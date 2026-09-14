using EventSourcing.Domain;
using EventSourcing.Projections;
using EventSourcing.Store;

namespace EventSourcing.Demos;

public static class Demos
{
    private static DateTimeOffset _clock = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset Tick() => _clock = _clock.AddMinutes(30);

    private static (InMemoryEventStore Store, string StreamId) BuildAccount()
    {
        var store = new InMemoryEventStore();
        const string streamId = "acct-1";

        var account = BankAccount.Open(streamId, "Ada Lovelace", Tick());
        account.Deposit(1_000m, Tick());
        account.Withdraw(250m, Tick());
        account.Deposit(5_000m, Tick());
        account.Withdraw(120m, Tick());

        store.Append(streamId, 0, account.UncommittedEvents);
        return (store, streamId);
    }

    public static Task Part1EventsAreTheTruth()
    {
        Console.WriteLine("=== PART 1: THE LOG IS THE TRUTH ===\n");

        var (store, streamId) = BuildAccount();

        Console.WriteLine("What was actually stored:\n");
        foreach (var stored in store.Read(streamId))
        {
            Console.WriteLine($"  v{stored.Version}  {stored.Event.GetType().Name}");
        }

        var account = BankAccount.Rehydrate(store.Read(streamId));

        Console.WriteLine($"\nState rebuilt from those events:");
        Console.WriteLine($"  owner={account.Owner} balance={account.Balance:C} version={account.Version}");
        Console.WriteLine("\nNo balance column exists anywhere. It is a fold over the history.");
        return Task.CompletedTask;
    }

    public static Task Part2TimeTravel()
    {
        Console.WriteLine("=== PART 2: STATE AT ANY POINT IN THE PAST ===\n");

        var (store, streamId) = BuildAccount();
        var all = store.Read(streamId);

        for (var version = 1; version <= all.Count; version++)
        {
            var asOf = BankAccount.Rehydrate(all.Take(version));
            Console.WriteLine($"  after v{version}: balance {asOf.Balance,10:C}   ({all[version - 1].Event.GetType().Name})");
        }

        Console.WriteLine("\nAsking 'what was the balance on Tuesday?' is just replaying fewer");
        Console.WriteLine("events. A system that stores only the current balance cannot answer it.");
        return Task.CompletedTask;
    }

    public static Task Part3Snapshots()
    {
        Console.WriteLine("=== PART 3: SNAPSHOTS ===\n");

        var store = new InMemoryEventStore();
        const string streamId = "acct-busy";

        var account = BankAccount.Open(streamId, "High-volume trader", Tick());
        for (var i = 0; i < 5_000; i++)
        {
            account.Deposit(10m, Tick());
        }
        store.Append(streamId, 0, account.UncommittedEvents);
        account.MarkCommitted();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var full = BankAccount.Rehydrate(store.Read(streamId));
        var fullTime = sw.Elapsed.TotalMilliseconds;

        var snapshot = full.TakeSnapshot();

        // More events arrive after the snapshot was taken.
        var live = BankAccount.Rehydrate(store.Read(streamId));
        live.Deposit(500m, Tick());
        store.Append(streamId, live.Version, live.UncommittedEvents);

        sw.Restart();
        var fromSnapshot = BankAccount.Rehydrate(snapshot, store.Read(streamId, snapshot.Version));
        var snapshotTime = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"  full replay:      {store.Read(streamId).Count,5} events, {fullTime:F2}ms");
        Console.WriteLine($"  from snapshot:    {store.Read(streamId, snapshot.Version).Count,5} events, {snapshotTime:F2}ms");
        Console.WriteLine($"  same balance:     {fromSnapshot.Balance == live.Balance} ({fromSnapshot.Balance:C})");
        Console.WriteLine("\nA snapshot is a cache of a fold, never a source of truth. Delete");
        Console.WriteLine("every snapshot and the system still works -- just more slowly.");
        return Task.CompletedTask;
    }

    public static Task Part4ProjectionsAnswerNewQuestions()
    {
        Console.WriteLine("=== PART 4: QUESTIONS YOU DID NOT PLAN FOR ===\n");

        var (store, streamId) = BuildAccount();

        var statement = new AccountStatement();
        foreach (var stored in store.Read(streamId)) statement.Project(stored);

        Console.WriteLine("Statement:\n");
        foreach (var line in statement.Lines)
        {
            Console.WriteLine($"  v{line.Version}  {line.Description,-32} {line.Amount,10:C}  {line.RunningBalance,10:C}");
        }

        // A report invented today, run over events recorded before it existed.
        var report = new LargeTransactionReport(threshold: 1_000m);
        foreach (var stored in store.Read(streamId)) report.Project(stored);

        Console.WriteLine($"\nCompliance report (>= $1,000), written after the fact:\n");
        foreach (var entry in report.Entries)
        {
            Console.WriteLine($"  v{entry.Version}  {entry.Kind,-12} {entry.Amount,10:C}  {entry.At:yyyy-MM-dd HH:mm}");
        }

        Console.WriteLine("\nThis report needed no migration and no new writes. A system storing");
        Console.WriteLine("only balances threw this information away at write time.");
        return Task.CompletedTask;
    }

    public static Task Part5OptimisticConcurrency()
    {
        Console.WriteLine("=== PART 5: TWO WRITERS, ONE STREAM ===\n");

        var (store, streamId) = BuildAccount();

        // Both load the same version.
        var userA = BankAccount.Rehydrate(store.Read(streamId));
        var userB = BankAccount.Rehydrate(store.Read(streamId));

        Console.WriteLine($"  both loaded version {userA.Version}, balance {userA.Balance:C}\n");

        userA.Withdraw(500m, Tick());
        store.Append(streamId, userA.Version, userA.UncommittedEvents);
        Console.WriteLine($"  user A withdrew $500 -- committed, stream now v{store.GetVersion(streamId)}");

        userB.Withdraw(5_000m, Tick());

        try
        {
            store.Append(streamId, userB.Version, userB.UncommittedEvents);
            Console.WriteLine("  user B committed -- this should not happen");
        }
        catch (ConcurrencyException ex)
        {
            Console.WriteLine($"  user B rejected: {ex.Message}");
        }

        Console.WriteLine("\nUser B must reload and decide again. Without this check both");
        Console.WriteLine("withdrawals would apply to a stale balance and overdraw the account.");
        return Task.CompletedTask;
    }
}
