using System.Data;

namespace Shared.Kernel.Persistence;

/// <summary>
/// Raises the isolation level of the transaction the request runs in.
///
/// Only needed where Read Committed is not enough. Two places in the product
/// qualify today, both for the same reason — two concurrent requests each read
/// a total, each find room, and each write: document allocation
/// (<c>AllocationService</c>) and converting a sales order to an invoice, where
/// the guard is "how much of this line has already been invoiced".
///
/// Put it on the action rather than the controller unless every action needs
/// it: Serializable makes Postgres abort one of two conflicting transactions
/// with 40001, and every read on the controller would then be a candidate.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class TransactionalAttribute : Attribute
{
    public TransactionalAttribute(IsolationLevel isolationLevel) => IsolationLevel = isolationLevel;

    public IsolationLevel IsolationLevel { get; }
}

/// <summary>
/// Keeps the request-wide transaction off this action.
///
/// For the handful of write endpoints where wrapping is wrong rather than
/// merely unnecessary: an action that must commit part of its work before
/// calling out (provisioning, which records a customer as Provisioning before
/// seeding so a crash is recoverable), a long import that commits in batches,
/// or an action that does no database work at all and should not take a
/// connection to prove it.
///
/// Reach for it rarely. The default is the safe one.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class NoTransactionAttribute : Attribute;
