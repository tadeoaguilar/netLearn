using EventSourcing.Store;

namespace EventSourcing.Domain;

public record AccountOpened(string AccountId, string Owner, DateTimeOffset OccurredAt) : IDomainEvent;
public record MoneyDeposited(decimal Amount, DateTimeOffset OccurredAt) : IDomainEvent;
public record MoneyWithdrawn(decimal Amount, DateTimeOffset OccurredAt) : IDomainEvent;
public record AccountFrozen(string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
public record AccountUnfrozen(DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Point-in-time state, so long streams need not be replayed from zero.</summary>
public record AccountSnapshot(string AccountId, string Owner, decimal Balance, bool IsFrozen, long Version);

/// <summary>
/// An aggregate whose state is REBUILT from its events rather than stored.
///
/// Note the two-step shape of every command: decide, then Apply. A command
/// checks the rules and raises an event; Apply only mutates state and must
/// never throw. That separation is what makes replaying history safe -- if
/// Apply could reject an event, a rule added today would make yesterday's
/// history unloadable.
/// </summary>
public class BankAccount
{
    private readonly List<IDomainEvent> _uncommitted = new();

    private BankAccount() { }

    public string AccountId { get; private set; } = string.Empty;
    public string Owner { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }
    public bool IsFrozen { get; private set; }
    public long Version { get; private set; }

    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommitted;
    public void MarkCommitted() => _uncommitted.Clear();

    public static BankAccount Open(string accountId, string owner, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new InvalidOperationException("Owner is required.");

        var account = new BankAccount();
        account.Raise(new AccountOpened(accountId, owner, now));
        return account;
    }

    /// <summary>Rebuild from history. This is the load path.</summary>
    public static BankAccount Rehydrate(IEnumerable<StoredEvent> history)
    {
        var account = new BankAccount();

        foreach (var stored in history)
        {
            account.Apply(stored.Event);
            account.Version = stored.Version;
        }

        return account;
    }

    /// <summary>Rebuild from a snapshot plus only the events after it.</summary>
    public static BankAccount Rehydrate(AccountSnapshot snapshot, IEnumerable<StoredEvent> since)
    {
        var account = new BankAccount
        {
            AccountId = snapshot.AccountId,
            Owner = snapshot.Owner,
            Balance = snapshot.Balance,
            IsFrozen = snapshot.IsFrozen,
            Version = snapshot.Version
        };

        foreach (var stored in since)
        {
            account.Apply(stored.Event);
            account.Version = stored.Version;
        }

        return account;
    }

    public AccountSnapshot TakeSnapshot() => new(AccountId, Owner, Balance, IsFrozen, Version);

    public void Deposit(decimal amount, DateTimeOffset now)
    {
        if (amount <= 0) throw new InvalidOperationException("Deposit must be positive.");
        if (IsFrozen) throw new InvalidOperationException("Account is frozen.");

        Raise(new MoneyDeposited(amount, now));
    }

    public void Withdraw(decimal amount, DateTimeOffset now)
    {
        if (amount <= 0) throw new InvalidOperationException("Withdrawal must be positive.");
        if (IsFrozen) throw new InvalidOperationException("Account is frozen.");
        if (amount > Balance) throw new InvalidOperationException($"Insufficient funds: balance is {Balance:C}.");

        Raise(new MoneyWithdrawn(amount, now));
    }

    public void Freeze(string reason, DateTimeOffset now)
    {
        if (IsFrozen) return;   // idempotent: freezing a frozen account is a no-op
        Raise(new AccountFrozen(reason, now));
    }

    public void Unfreeze(DateTimeOffset now)
    {
        if (!IsFrozen) return;
        Raise(new AccountUnfrozen(now));
    }

    private void Raise(IDomainEvent @event)
    {
        Apply(@event);
        _uncommitted.Add(@event);
    }

    /// <summary>
    /// State transitions only. No validation, no throwing -- this runs against
    /// events that were already accepted, possibly years ago.
    /// </summary>
    private void Apply(IDomainEvent @event)
    {
        switch (@event)
        {
            case AccountOpened opened:
                AccountId = opened.AccountId;
                Owner = opened.Owner;
                break;
            case MoneyDeposited deposited:
                Balance += deposited.Amount;
                break;
            case MoneyWithdrawn withdrawn:
                Balance -= withdrawn.Amount;
                break;
            case AccountFrozen:
                IsFrozen = true;
                break;
            case AccountUnfrozen:
                IsFrozen = false;
                break;
        }
    }
}
