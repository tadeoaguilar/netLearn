namespace EfCoreTransactions.Domain;

/// <summary>
/// The lifecycle of a <see cref="Transfer"/>. A transfer is written as
/// <see cref="Pending"/> before either leg of the money movement happens, and
/// flipped to <see cref="Completed"/> or <see cref="Failed"/> once the
/// enclosing transaction commits or rolls back -- see Part 2 and Part 6 of
/// EXERCISE.md.
/// </summary>
public enum TransferStatus
{
    Pending,
    Completed,
    Failed
}
